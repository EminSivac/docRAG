using System.Security.Cryptography;
using DocRag.Server.Clients;
using DocRag.Server.Ingestion.Parsers;
using DocRag.Shared;

namespace DocRag.Server.Ingestion;

public sealed class IngestionService
{
    private readonly AppConfig _config;
    private readonly IndexManifest _manifest;
    private readonly QdrantService _qdrant;
    private readonly LmStudioClient _lmStudio;
    private readonly FileCrawler _crawler = new();
    private readonly Chunker _chunker;
    private readonly List<IDocumentParser> _parsers;
    private readonly ILogger<IngestionService> _logger;

    private readonly SemaphoreSlim _runLock = new(1, 1);
    private IngestStatus _status = new();
    private readonly object _statusLock = new();
    private readonly SemaphoreSlim _ensureLock = new(1, 1);
    private volatile bool _collectionEnsured;
    private SemaphoreSlim _embedGate = new(1, 1);

    public IngestionService(
        AppConfig config,
        IndexManifest manifest,
        QdrantService qdrant,
        LmStudioClient lmStudio,
        ILogger<IngestionService> logger)
    {
        _config = config;
        _manifest = manifest;
        _qdrant = qdrant;
        _lmStudio = lmStudio;
        _logger = logger;
        _chunker = new Chunker(config.ChunkSizeChars, config.ChunkOverlapChars);
        _parsers = new List<IDocumentParser> { new PdfParser(), new OfficeParser(), new TextParser() };
    }

    public IngestStatus GetStatus() => _status;

    /// <summary>
    /// Wipes everything: all Qdrant collections belonging to this app (across all embedding
    /// models, not just the currently configured one) and the local ingestion manifest.
    /// Refuses while a run is in progress.
    /// </summary>
    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        if (!await _runLock.WaitAsync(0, ct))
        {
            throw new InvalidOperationException("Es läuft gerade eine Indexierung. Bitte warten, bis sie fertig ist.");
        }

        try
        {
            var collections = await _qdrant.ListCollectionsWithPrefixAsync(_config.QdrantCollectionName, ct);
            foreach (var collection in collections)
            {
                await _qdrant.DeleteCollectionAsync(collection, ct);
            }

            _manifest.ClearAll();
            _status = new IngestStatus();
            _logger.LogInformation("Datenbank vollständig geleert ({Count} Qdrant-Collection(s) entfernt).", collections.Count);
        }
        finally
        {
            _runLock.Release();
        }
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        if (!await _runLock.WaitAsync(0, ct))
        {
            throw new InvalidOperationException("Es läuft bereits ein Indexierungslauf.");
        }

        _status = new IngestStatus { IsRunning = true, StartedAt = DateTime.UtcNow };

        try
        {
            var files = _crawler.EnumerateFiles(_config.DocumentRoots, _logger).ToList();
            _status.TotalFiles = files.Count;

            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in files)
            {
                seenPaths.Add(filePath);
            }

            _collectionEnsured = false;
            var parallelism = Math.Max(1, _config.EmbeddingMaxParallelism);
            _embedGate = new SemaphoreSlim(parallelism, parallelism);

            // Files run in parallel so parsing/hashing/Qdrant writes of one file overlap with
            // embedding requests of another. The shared _embedGate caps the total number of
            // in-flight embedding requests to EmbeddingMaxParallelism across all files.
            await Parallel.ForEachAsync(
                files,
                new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
                async (filePath, token) =>
                {
                    _status.CurrentFile = filePath;

                    try
                    {
                        var wasProcessed = await ProcessFileAsync(filePath, token);
                        lock (_statusLock)
                        {
                            if (wasProcessed)
                            {
                                _status.ProcessedFiles++;
                            }
                            else
                            {
                                _status.SkippedFiles++;
                            }
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lock (_statusLock)
                        {
                            _status.FailedFiles++;
                            _status.RecentErrors.Add($"{filePath}: {ex.Message}");
                            if (_status.RecentErrors.Count > 50)
                            {
                                _status.RecentErrors.RemoveAt(0);
                            }
                        }
                        _logger.LogError(ex, "Fehler beim Verarbeiten von {File}", filePath);
                    }
                });

            // Remove documents that were deleted from disk since the last run
            foreach (var trackedPath in _manifest.GetAllTrackedPaths(_config.EmbeddingModel))
            {
                if (!seenPaths.Contains(trackedPath))
                {
                    await _qdrant.DeleteByFilePathAsync(trackedPath, ct);
                    _manifest.Remove(trackedPath, _config.EmbeddingModel);
                }
            }

            _status.IsRunning = false;
            _status.CurrentFile = null;
            _status.FinishedAt = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            _status.IsRunning = false;
            _status.FinishedAt = DateTime.UtcNow;
            throw;
        }
        catch (Exception ex)
        {
            _status.IsRunning = false;
            _status.FinishedAt = DateTime.UtcNow;
            _status.RecentErrors.Add($"Indexierungslauf abgebrochen: {ex.Message}");
            _logger.LogError(ex, "Indexierungslauf fehlgeschlagen");
        }
        finally
        {
            _runLock.Release();
        }
    }

    private async Task<bool> ProcessFileAsync(string filePath, CancellationToken ct)
    {
        var fileInfo = new FileInfo(filePath);
        var lastModifiedUtc = fileInfo.LastWriteTimeUtc;
        var existing = _manifest.GetRecord(filePath, _config.EmbeddingModel);

        // Cheap fast path: unchanged mtime means we skip re-hashing the whole file.
        if (existing is not null && existing.LastModifiedUtc == lastModifiedUtc)
        {
            return false;
        }

        var contentHash = ComputeHash(filePath);
        if (existing is not null && existing.ContentHash == contentHash)
        {
            _manifest.Upsert(filePath, _config.EmbeddingModel, contentHash, lastModifiedUtc, existing.ChunkCount);
            return false;
        }

        var parser = _parsers.FirstOrDefault(p => p.CanParse(filePath));
        if (parser is null)
        {
            return false;
        }

        // Re-indexing: drop old chunks for this file first
        if (existing is not null)
        {
            await _qdrant.DeleteByFilePathAsync(filePath, ct);
        }

        var document = parser.Parse(filePath);
        var chunks = _chunker.ChunkDocument(document).ToList();

        var fileName = Path.GetFileName(filePath);
        var chunkCount = 0;

        // Batches of one file are embedded and upserted concurrently; _embedGate limits
        // how many embedding requests hit LM Studio at the same time.
        var batchTasks = chunks
            .Chunk(Math.Max(1, _config.EmbeddingBatchSize))
            .Select(batch => ProcessBatchAsync(batch, filePath, fileName, ct))
            .ToList();

        var counts = await Task.WhenAll(batchTasks);
        chunkCount = counts.Sum();

        _manifest.Upsert(filePath, _config.EmbeddingModel, contentHash, lastModifiedUtc, chunkCount);
        lock (_statusLock)
        {
            _status.TotalChunksIndexed += chunkCount;
        }
        return true;
    }

    private async Task<int> ProcessBatchAsync(TextChunk[] batch, string filePath, string fileName, CancellationToken ct)
    {
        var texts = batch.Select(c => c.Text).ToList();

        List<float[]> embeddings;
        var gate = _embedGate;
        await gate.WaitAsync(ct);
        try
        {
            embeddings = await _lmStudio.GetEmbeddingsAsync(texts, ct);
        }
        finally
        {
            gate.Release();
        }

        var points = new List<QdrantChunkPoint>(batch.Length);
        for (var i = 0; i < batch.Length; i++)
        {
            points.Add(new QdrantChunkPoint
            {
                Id = Guid.NewGuid(),
                Vector = embeddings[i],
                FilePath = filePath,
                FileName = fileName,
                Location = batch[i].Location,
                Text = batch[i].Text,
                SectionId = batch[i].SectionId,
                ChunkIndex = batch[i].ChunkIndex
            });
        }

        if (points.Count > 0)
        {
            await EnsureCollectionOnceAsync((ulong)points[0].Vector.Length, ct);
        }

        await _qdrant.UpsertAsync(points, ct);
        return points.Count;
    }

    private async Task EnsureCollectionOnceAsync(ulong vectorSize, CancellationToken ct)
    {
        if (_collectionEnsured)
        {
            return;
        }

        await _ensureLock.WaitAsync(ct);
        try
        {
            if (_collectionEnsured)
            {
                return;
            }

            await _qdrant.EnsureCollectionAsync(vectorSize, ct);
            _collectionEnsured = true;
        }
        finally
        {
            _ensureLock.Release();
        }
    }

    private static string ComputeHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }
}
