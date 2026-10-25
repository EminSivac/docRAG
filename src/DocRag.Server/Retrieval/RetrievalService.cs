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

    public async Task<ChatResponse> AnswerAsync(string question, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return new ChatResponse { Answer = "Bitte eine Frage eingeben.", Sources = new List<SourceCitation>() };
        }

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

        var userPrompt = PromptBuilder.BuildUserPrompt(question, results);
        var answer = await _lmStudio.GetChatCompletionAsync(PromptBuilder.SystemPrompt, userPrompt, ct);

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
}
