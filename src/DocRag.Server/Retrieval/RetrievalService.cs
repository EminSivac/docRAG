using DocRag.Server.Clients;
using DocRag.Shared;

namespace DocRag.Server.Retrieval;

public sealed class RetrievalService
{
    private readonly QdrantService _qdrant;
    private readonly LmStudioClient _lmStudio;
    private readonly AppConfig _config;

    public RetrievalService(QdrantService qdrant, LmStudioClient lmStudio, AppConfig config)
    {
        _qdrant = qdrant;
        _lmStudio = lmStudio;
        _config = config;
    }

    private const int DeepMaxSections = 10;
    private const int DeepMaxQueries = 4;

    private (int Tokens, DateTime At)? _contextCache;

    /// <summary>Context window of the chat model: configured value, else detected from LM Studio, else 8k.</summary>
    private async Task<int> GetContextTokensAsync(CancellationToken ct)
    {
        if (_config.ChatContextTokens > 0) return _config.ChatContextTokens;
        if (_contextCache is { } cached && DateTime.UtcNow - cached.At < TimeSpan.FromMinutes(2)) return cached.Tokens;

        var info = await LmStudioProbe.FindChatModelAsync(_config.LmStudioBaseUrl, _config.ChatModel, ct);
        if (info is { IsLoaded: true, LoadedContext: > 0 })
        {
            _contextCache = (info.LoadedContext.Value, DateTime.UtcNow);
            return info.LoadedContext.Value;
        }

        return ContextOptimizer.FallbackContextTokens;
    }

    public async Task<ChatResponse> AnswerAsync(string question, bool deep = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return new ChatResponse { Answer = "Bitte eine Frage eingeben.", Sources = new List<SourceCitation>() };
        }

        var contextTokens = await GetContextTokensAsync(ct);
        var maxOutput = ContextOptimizer.OutputTokensFor(contextTokens);
        // The configured budget is a cap; never exceed what actually fits into the model's context window.
        var budget = Math.Max(1000, Math.Min(_config.RetrievalMaxContextChars, ContextOptimizer.MaxContextCharsFor(contextTokens)));

        if (deep) return await AnswerDeepAsync(question, budget, maxOutput, ct);

        var questionEmbedding = await _lmStudio.GetEmbeddingsAsync(new[] { question }, ct);
        var results = await _qdrant.SearchAsync(questionEmbedding[0], (ulong)_config.RetrievalTopK, ct);

        if (results.Count == 0)
        {
            return new ChatResponse
            {
                Answer = "Ich habe dazu nichts in den indizierten Dokumenten gefunden.",
                Sources = new List<SourceCitation>()
            };
        }

        var context = await ExpandToSectionsAsync(results, budget, int.MaxValue, ct);
        var userPrompt = PromptBuilder.BuildUserPrompt(question, context);
        var answer = await _lmStudio.GetChatCompletionAsync(PromptBuilder.SystemPrompt, userPrompt, ct, maxOutput);

        var sources = results.Select(r => new SourceCitation
        {
            FileName = r.FileName,
            FilePath = r.FilePath,
            Location = r.Location,
            Snippet = r.Text.Length > 300 ? r.Text[..300] + "…" : r.Text,
            Score = r.Score
        }).ToList();

        return new ChatResponse { Answer = answer, Sources = sources };
    }

    /// <summary>
    /// Deep research: plan several search queries, retrieve for each, read more sections than fit into one
    /// prompt (map step: extract relevant facts per batch) and write the answer from those notes (reduce step).
    /// </summary>
    private async Task<ChatResponse> AnswerDeepAsync(string question, int budget, int maxOutput, CancellationToken ct)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        // 1. Plan: the question itself plus a few LLM-generated sub-queries.
        var queries = new List<string> { question };
        try
        {
            var plan = await _lmStudio.GetChatCompletionAsync(
                PromptBuilder.PlannerSystemPrompt, PromptBuilder.BuildPlannerPrompt(question), ct, 300, 0.3);
            queries.AddRange(ParseQueries(plan, question));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Planning is optional: continue with the original question only.
        }

        // 2. Retrieve for every query and merge by section; sections hit by several queries rank higher.
        var embeddings = await _lmStudio.GetEmbeddingsAsync(queries, ct);
        var topK = (ulong)Math.Max(_config.RetrievalTopK, 4);
        var merged = new Dictionary<(string, int), (QdrantSearchResult Hit, int Count)>();
        foreach (var embedding in embeddings)
        {
            foreach (var hit in await _qdrant.SearchAsync(embedding, topK, ct))
            {
                var key = hit.SectionId >= 0 ? (hit.FilePath, hit.SectionId) : (hit.FilePath, -1 - hit.ChunkIndex);
                if (merged.TryGetValue(key, out var existing))
                {
                    merged[key] = (existing.Hit.Score >= hit.Score ? existing.Hit : hit, existing.Count + 1);
                }
                else
                {
                    merged[key] = (hit, 1);
                }
            }
        }

        if (merged.Count == 0)
        {
            return new ChatResponse
            {
                Answer = "Ich habe dazu nichts in den indizierten Dokumenten gefunden.",
                Deep = true,
                Queries = queries,
                ElapsedSeconds = watch.Elapsed.TotalSeconds
            };
        }

        var ranked = merged.Values
            .OrderByDescending(m => m.Hit.Score + 0.02f * (m.Count - 1))
            .Take(DeepMaxSections)
            .Select(m => m.Hit)
            .ToList();

        // 3. Read: whole sections, capped per section so many different places fit in.
        var perSection = Math.Max(2000, budget / 2);
        var evidence = await ExpandToSectionsAsync(ranked, perSection * ranked.Count, perSection, ct);
        var numbered = evidence.Select((chunk, i) => (Number: i + 1, Chunk: chunk)).ToList();

        // 4. Map: split the evidence into batches that fit the context and extract the relevant facts.
        var batches = new List<List<(int Number, QdrantSearchResult Chunk)>>();
        var current = new List<(int Number, QdrantSearchResult Chunk)>();
        var currentSize = 0;
        foreach (var item in numbered)
        {
            if (current.Count > 0 && currentSize + item.Chunk.Text.Length > budget)
            {
                batches.Add(current);
                current = new List<(int, QdrantSearchResult)>();
                currentSize = 0;
            }

            current.Add(item);
            currentSize += item.Chunk.Text.Length;
        }

        if (current.Count > 0) batches.Add(current);

        string answer;
        if (batches.Count == 1)
        {
            var prompt = PromptBuilder.BuildNumberedExcerpts(question, batches[0]);
            answer = await _lmStudio.GetChatCompletionAsync(PromptBuilder.SynthesisSystemPromptDirect, prompt, ct, maxOutput);
        }
        else
        {
            var noteTokens = Math.Clamp((int)(budget * 0.6 / batches.Count / 3), 200, maxOutput);
            var notes = new System.Text.StringBuilder();
            foreach (var batch in batches)
            {
                var prompt = PromptBuilder.BuildNumberedExcerpts(question, batch);
                var note = await _lmStudio.GetChatCompletionAsync(PromptBuilder.ExtractSystemPrompt, prompt, ct, noteTokens, 0.1);
                if (string.IsNullOrWhiteSpace(note) || note.Trim().StartsWith("KEINE", StringComparison.OrdinalIgnoreCase)) continue;
                notes.AppendLine(note.Trim()).AppendLine();
            }

            answer = notes.Length == 0
                ? "Ich habe in den durchsuchten Dokumenten nichts Relevantes zu dieser Frage gefunden."
                : await _lmStudio.GetChatCompletionAsync(
                    PromptBuilder.SynthesisSystemPrompt, PromptBuilder.BuildSynthesisPrompt(question, notes.ToString()), ct, maxOutput);
        }

        var sources = evidence.Select(e => new SourceCitation
        {
            FileName = e.FileName,
            FilePath = e.FilePath,
            Location = e.Location,
            Snippet = e.Text.Length > 300 ? e.Text[..300] + "…" : e.Text,
            Score = e.Score
        }).ToList();

        return new ChatResponse
        {
            Answer = answer,
            Sources = sources,
            Deep = true,
            Queries = queries,
            ElapsedSeconds = watch.Elapsed.TotalSeconds
        };
    }

    private static IEnumerable<string> ParseQueries(string plan, string question)
    {
        return plan.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => System.Text.RegularExpressions.Regex.Replace(line, @"^(?:[-*•]|\d+[.)])\s*", "").Trim('"', ' ', '*'))
            .Where(line => line.Length is >= 3 and <= 200)
            .Where(line => !string.Equals(line, question, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(DeepMaxQueries);
    }

    /// <summary>
    /// Replaces each hit chunk with its whole section (parent-document retrieval). Sections are added in
    /// order of best score until the character budget is used up; an oversized section is trimmed to a
    /// window of neighbouring chunks around the hit (at most <paramref name="perSectionCap"/> characters).
    /// </summary>
    private async Task<List<QdrantSearchResult>> ExpandToSectionsAsync(
        List<QdrantSearchResult> hits, int budget, int perSectionCap, CancellationToken ct)
    {
        var used = 0;
        var seen = new HashSet<(string, int)>();
        var context = new List<QdrantSearchResult>();

        foreach (var hit in hits.OrderByDescending(h => h.Score))
        {
            var remaining = Math.Min(budget - used, perSectionCap);
            if (budget - used <= 0) break;

            if (hit.SectionId < 0)
            {
                // Chunk indexed before section ids existed: use it as is.
                if (hit.Text.Length <= remaining)
                {
                    context.Add(hit);
                    used += hit.Text.Length;
                }
                continue;
            }

            if (!seen.Add((hit.FilePath, hit.SectionId))) continue;

            var chunks = await _qdrant.GetSectionChunksAsync(hit.FilePath, hit.SectionId, ct);
            if (chunks.Count == 0) chunks = new List<QdrantSearchResult> { hit };

            var text = MergeChunks(SelectWindow(chunks, hit.ChunkIndex, remaining));
            context.Add(new QdrantSearchResult
            {
                Score = hit.Score,
                FilePath = hit.FilePath,
                FileName = hit.FileName,
                Location = hit.Location,
                Text = text,
                SectionId = hit.SectionId,
                ChunkIndex = hit.ChunkIndex
            });
            used += text.Length;
        }

        return context;
    }

    private static List<QdrantSearchResult> SelectWindow(List<QdrantSearchResult> chunks, int hitChunkIndex, int budget)
    {
        if (chunks.Sum(c => c.Text.Length) <= budget) return chunks;

        var center = Math.Max(0, chunks.FindIndex(c => c.ChunkIndex == hitChunkIndex));
        int lo = center, hi = center;
        var size = chunks[center].Text.Length;

        // Grow the window around the hit, alternating after/before, until the budget is reached.
        var growAfter = true;
        while (true)
        {
            var canAfter = hi + 1 < chunks.Count && size + chunks[hi + 1].Text.Length <= budget;
            var canBefore = lo - 1 >= 0 && size + chunks[lo - 1].Text.Length <= budget;
            if (!canAfter && !canBefore) break;

            if ((growAfter && canAfter) || !canBefore)
            {
                hi++;
                size += chunks[hi].Text.Length;
            }
            else
            {
                lo--;
                size += chunks[lo].Text.Length;
            }

            growAfter = !growAfter;
        }

        return chunks.GetRange(lo, hi - lo + 1);
    }

    /// <summary>Joins consecutive chunks: keeps the heading prefix once and removes the chunk-overlap duplicates.</summary>
    private static string MergeChunks(List<QdrantSearchResult> chunks)
    {
        var sb = new System.Text.StringBuilder();
        var previousBody = "";

        for (var i = 0; i < chunks.Count; i++)
        {
            var text = chunks[i].Text;
            var heading = "";
            if (text.StartsWith('['))
            {
                var nl = text.IndexOf('\n');
                if (nl > 0 && text[nl - 1] == ']')
                {
                    heading = text[..nl];
                    text = text[(nl + 1)..];
                }
            }

            if (i == 0)
            {
                if (heading.Length > 0) sb.Append(heading).Append('\n');
            }
            else
            {
                text = TrimOverlap(previousBody, text);
                sb.Append("\n\n");
            }

            sb.Append(text);
            previousBody = chunks[i].Text;
        }

        return sb.ToString();
    }

    private static string TrimOverlap(string previous, string next)
    {
        // The chunker repeats the tail of the previous chunk at the start of the next one.
        var max = Math.Min(Math.Min(previous.Length, next.Length), 400);
        for (var k = max; k >= 20; k--)
        {
            if (string.CompareOrdinal(previous, previous.Length - k, next, 0, k) == 0)
            {
                return next[k..].TrimStart();
            }
        }

        return next;
    }
}
