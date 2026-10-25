using System.Text.Json;

namespace DocRag.Shared;

public sealed record ContextSettings(int ContextTokens, int MaxOutputTokens, int RetrievalMaxContextChars, int RetrievalTopK);

/// <summary>Derives the retrieval budget from the context window the chat model is loaded with.</summary>
public static class ContextOptimizer
{
    public const int FallbackContextTokens = 8192;

    private const int PromptOverheadTokens = 500;
    private const double CharsPerToken = 3.0; // conservative for German text
    private const double SafetyFactor = 0.9;

    public static int OutputTokensFor(int contextTokens) => Math.Clamp(contextTokens / 4, 512, 4096);

    public static int MaxContextCharsFor(int contextTokens)
    {
        var inputTokens = contextTokens - OutputTokensFor(contextTokens) - PromptOverheadTokens;
        return Math.Max(1500, (int)(inputTokens * CharsPerToken * SafetyFactor));
    }

    public static ContextSettings Compute(int contextTokens)
    {
        var chars = MaxContextCharsFor(contextTokens);
        return new ContextSettings(
            contextTokens,
            OutputTokensFor(contextTokens),
            chars,
            Math.Clamp(chars / 2000, 3, 15));
    }
}

public sealed record LmModelInfo(string Id, string? Type, string? State, int? LoadedContext, int? MaxContext)
{
    public bool IsLoaded => string.Equals(State, "loaded", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Reads model state (incl. context length) from LM Studio's native REST API.</summary>
public static class LmStudioProbe
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>
    /// Finds the configured chat model (falls back to any loaded LLM when the name doesn't match).
    /// Returns null when LM Studio is unreachable or lists no suitable model.
    /// </summary>
    public static async Task<LmModelInfo?> FindChatModelAsync(string baseUrl, string chatModel, CancellationToken ct = default)
    {
        List<LmModelInfo> models;
        try
        {
            var json = await Http.GetStringAsync(baseUrl.TrimEnd('/') + "/api/v0/models", ct);
            using var doc = JsonDocument.Parse(json);
            models = doc.RootElement.GetProperty("data").EnumerateArray().Select(Parse).ToList();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }

        var llms = models.Where(m => m.Type is null or "llm" or "vlm").ToList();
        return llms.FirstOrDefault(m => string.Equals(m.Id, chatModel, StringComparison.OrdinalIgnoreCase))
               ?? llms.FirstOrDefault(m => m.IsLoaded);
    }

    private static LmModelInfo Parse(JsonElement item)
    {
        string? Str(string name) => item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        int? Int(string name) => item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

        return new LmModelInfo(Str("id") ?? "", Str("type"), Str("state"), Int("loaded_context_length"), Int("max_context_length"));
    }
}
