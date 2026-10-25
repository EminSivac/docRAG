using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocRag.Server.Clients;

public sealed class LmStudioClient
{
    private readonly HttpClient _http;
    private readonly string _embeddingModel;
    private readonly string _chatModel;

    public LmStudioClient(HttpClient http, string baseUrl, string embeddingModel, string chatModel)
    {
        _http = http;
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromMinutes(5);
        _embeddingModel = embeddingModel;
        _chatModel = chatModel;
    }

    public async Task<List<float[]>> GetEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken ct = default)
    {
        var requestBody = new EmbeddingRequest
        {
            Model = _embeddingModel,
            Input = inputs.ToArray()
        };

        using var response = await _http.PostAsJsonAsync("v1/embeddings", requestBody, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Leere Antwort von LM Studio (Embeddings).");

        return payload.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToList();
    }

    public async Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var requestBody = new ChatCompletionRequest
        {
            Model = _chatModel,
            Messages = new[]
            {
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt }
            },
            Temperature = 0.2
        };

        using var response = await _http.PostAsJsonAsync("v1/chat/completions", requestBody, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Leere Antwort von LM Studio (Chat).");

        return payload.Choices.FirstOrDefault()?.Message.Content?.Trim() ?? "";
    }

    private sealed class EmbeddingRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("input")] public string[] Input { get; set; } = Array.Empty<string>();
    }

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")] public List<EmbeddingData> Data { get; set; } = new();
    }

    private sealed class EmbeddingData
    {
        [JsonPropertyName("embedding")] public float[] Embedding { get; set; } = Array.Empty<float>();
        [JsonPropertyName("index")] public int Index { get; set; }
    }

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("messages")] public ChatMessage[] Messages { get; set; } = Array.Empty<ChatMessage>();
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")] public List<ChatChoice> Choices { get; set; } = new();
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")] public ChatMessage Message { get; set; } = new();
    }
}
