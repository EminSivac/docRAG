using System.Diagnostics;
using System.Net.Http.Json;

namespace DocRag.Admin;

public sealed record BenchmarkRun(int BatchSize, int Parallelism, double ChunksPerSecond);

public sealed record BenchmarkResult(int BatchSize, int Parallelism, double ChunksPerSecond, IReadOnlyList<BenchmarkRun> Runs);

/// <summary>
/// Measures embedding throughput against LM Studio for different batch sizes and parallelism
/// levels. Two stages keep the number of runs small: first the batch size is searched with a
/// single request in flight, then the parallelism is searched using the best batch size.
/// </summary>
public static class EmbeddingBenchmark
{
    private static readonly int[] BatchSizes = { 8, 16, 32, 64 };
    private static readonly int[] ParallelismLevels = { 1, 2, 4 };
    private static readonly TimeSpan TimeBudget = TimeSpan.FromMinutes(3);

    public static async Task<BenchmarkResult> RunAsync(
        string baseUrl,
        string model,
        int chunkChars,
        IProgress<string> progress,
        CancellationToken ct)
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(5)
        };

        var deadline = Stopwatch.StartNew();
        var runs = new List<BenchmarkRun>();
        var random = new Random(42);

        progress.Report("Wärme Modell auf …");
        await EmbedAsync(http, model, MakeTexts(random, 4, chunkChars), ct);

        // Stage 1: batch size, one request at a time
        foreach (var batch in BatchSizes)
        {
            if (deadline.Elapsed > TimeBudget && runs.Count > 0)
            {
                break;
            }

            progress.Report($"Teste Batchgröße {batch} …");
            var run = await MeasureAsync(http, model, random, chunkChars, batch, 1, ct);
            if (run is null)
            {
                break; // larger batches will fail too (context/memory limit)
            }

            runs.Add(run);
        }

        if (runs.Count == 0)
        {
            throw new InvalidOperationException("Kein Test war erfolgreich. Läuft LM Studio und ist das Embedding-Modell geladen?");
        }

        var bestBatch = Pick(runs).BatchSize;

        // Stage 2: parallelism with the best batch size (1 is already measured)
        foreach (var parallelism in ParallelismLevels.Where(p => p > 1))
        {
            if (deadline.Elapsed > TimeBudget)
            {
                break;
            }

            progress.Report($"Teste {parallelism} parallele Anfragen (Batch {bestBatch}) …");
            var run = await MeasureAsync(http, model, random, chunkChars, bestBatch, parallelism, ct);
            if (run is null)
            {
                break;
            }

            runs.Add(run);
        }

        var best = Pick(runs);
        return new BenchmarkResult(best.BatchSize, best.Parallelism, best.ChunksPerSecond, runs);
    }

    /// <summary>Fastest run; among runs within 5% of it, the cheapest one (smallest batch x parallelism).</summary>
    private static BenchmarkRun Pick(List<BenchmarkRun> runs)
    {
        var fastest = runs.Max(r => r.ChunksPerSecond);
        return runs
            .Where(r => r.ChunksPerSecond >= fastest * 0.95)
            .OrderBy(r => r.BatchSize * r.Parallelism)
            .ThenByDescending(r => r.ChunksPerSecond)
            .First();
    }

    private static async Task<BenchmarkRun?> MeasureAsync(
        HttpClient http, string model, Random random, int chunkChars, int batchSize, int parallelism, CancellationToken ct)
    {
        // Enough batches that every parallel slot gets at least two requests
        var batches = Math.Max(4, parallelism * 2);
        var inputs = Enumerable.Range(0, batches).Select(_ => MakeTexts(random, batchSize, chunkChars)).ToList();

        try
        {
            var sw = Stopwatch.StartNew();
            await Parallel.ForEachAsync(
                inputs,
                new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
                async (texts, token) => await EmbedAsync(http, model, texts, token));
            sw.Stop();

            return new BenchmarkRun(batchSize, parallelism, batches * batchSize / sw.Elapsed.TotalSeconds);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static async Task EmbedAsync(HttpClient http, string model, string[] texts, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("v1/embeddings", new { model, input = texts }, ct);
        response.EnsureSuccessStatusCode();
        await response.Content.ReadAsStringAsync(ct);
    }

    // Random words so LM Studio can't serve repeated inputs from a cache.
    private static string[] MakeTexts(Random random, int count, int chunkChars)
    {
        var texts = new string[count];
        for (var i = 0; i < count; i++)
        {
            var sb = new System.Text.StringBuilder(chunkChars + 16);
            while (sb.Length < chunkChars)
            {
                var len = random.Next(3, 10);
                for (var c = 0; c < len; c++)
                {
                    sb.Append((char)('a' + random.Next(26)));
                }

                sb.Append(' ');
            }

            texts[i] = sb.ToString();
        }

        return texts;
    }
}
