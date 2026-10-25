using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace DocRag.Server.Clients;

public sealed class QdrantChunkPoint
{
    public required Guid Id { get; init; }
    public required float[] Vector { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string Location { get; init; }
    public required string Text { get; init; }
    public required int SectionId { get; init; }
    public required int ChunkIndex { get; init; }
}

public sealed class QdrantSearchResult
{
    public required float Score { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string Location { get; init; }
    public required string Text { get; init; }
    /// <summary>-1 for chunks indexed before section ids existed.</summary>
    public int SectionId { get; init; } = -1;
    public int ChunkIndex { get; init; }
}

public sealed class QdrantService
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;

    public QdrantService(string host, int grpcPort, string collectionName)
    {
        _client = new QdrantClient(host, grpcPort);
        _collectionName = collectionName;
    }

    public async Task EnsureCollectionAsync(ulong vectorSize, CancellationToken ct = default)
    {
        var exists = await _client.CollectionExistsAsync(_collectionName, ct);
        if (exists)
        {
            return;
        }

        await _client.CreateCollectionAsync(
            _collectionName,
            new VectorParams { Size = vectorSize, Distance = Distance.Cosine },
            cancellationToken: ct);
    }

    public async Task UpsertAsync(IReadOnlyList<QdrantChunkPoint> points, CancellationToken ct = default)
    {
        if (points.Count == 0)
        {
            return;
        }

        var grpcPoints = points.Select(p =>
        {
            var point = new PointStruct
            {
                Id = new PointId { Uuid = p.Id.ToString() },
                Vectors = p.Vector
            };
            point.Payload.Add("file_path", p.FilePath);
            point.Payload.Add("file_name", p.FileName);
            point.Payload.Add("location", p.Location);
            point.Payload.Add("text", p.Text);
            point.Payload.Add("section_id", (long)p.SectionId);
            point.Payload.Add("chunk_index", (long)p.ChunkIndex);
            return point;
        }).ToList();

        await _client.UpsertAsync(_collectionName, grpcPoints, cancellationToken: ct);
    }

    public async Task DeleteByFilePathAsync(string filePath, CancellationToken ct = default)
    {
        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "file_path",
                        Match = new Match { Keyword = filePath }
                    }
                }
            }
        };

        await _client.DeleteAsync(_collectionName, filter, cancellationToken: ct);
    }

    public async Task<List<string>> ListCollectionsWithPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var collections = await _client.ListCollectionsAsync(ct);
        return collections
            .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default)
    {
        await _client.DeleteCollectionAsync(collectionName, cancellationToken: ct);
    }

    public async Task<List<QdrantSearchResult>> SearchAsync(float[] queryVector, ulong limit, CancellationToken ct = default)
    {
        var results = await _client.QueryAsync(
            _collectionName,
            query: queryVector,
            limit: limit,
            payloadSelector: true,
            cancellationToken: ct);

        return results.Select(r => ToResult(r.Score, r.Payload)).ToList();
    }

    /// <summary>All chunks of one section of a file, in document order.</summary>
    public async Task<List<QdrantSearchResult>> GetSectionChunksAsync(string filePath, int sectionId, CancellationToken ct = default)
    {
        var filter = new Filter
        {
            Must =
            {
                new Condition { Field = new FieldCondition { Key = "file_path", Match = new Match { Keyword = filePath } } },
                new Condition { Field = new FieldCondition { Key = "section_id", Match = new Match { Integer = sectionId } } }
            }
        };

        var response = await _client.ScrollAsync(_collectionName, filter, limit: 256, payloadSelector: true, cancellationToken: ct);
        return response.Result
            .Select(r => ToResult(0, r.Payload))
            .OrderBy(r => r.ChunkIndex)
            .ToList();
    }

    private static QdrantSearchResult ToResult(float score, Google.Protobuf.Collections.MapField<string, Value> payload) => new()
    {
        Score = score,
        FilePath = payload.TryGetValue("file_path", out var fp) ? fp.StringValue : "",
        FileName = payload.TryGetValue("file_name", out var fn) ? fn.StringValue : "",
        Location = payload.TryGetValue("location", out var loc) ? loc.StringValue : "",
        Text = payload.TryGetValue("text", out var text) ? text.StringValue : "",
        SectionId = payload.TryGetValue("section_id", out var sid) ? (int)sid.IntegerValue : -1,
        ChunkIndex = payload.TryGetValue("chunk_index", out var ci) ? (int)ci.IntegerValue : 0
    };
}
