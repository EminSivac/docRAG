using DocRag.Server.Clients;

namespace DocRag.Server.Retrieval;

public static class PromptBuilder
{
    public const string SystemPrompt =
        "Du bist ein Assistent, der ausschließlich auf Basis der bereitgestellten Dokumentauszüge antwortet. " +
        "Wenn die Antwort nicht in den Auszügen enthalten ist, sag klar, dass du es in den Dokumenten nicht findest. " +
        "Erfinde keine Informationen. Antworte auf Deutsch, prägnant und mit Bezug auf die relevanten Auszüge.";

    public static string BuildUserPrompt(string question, IReadOnlyList<QdrantSearchResult> contextChunks)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Dokumentauszüge:");
        for (var i = 0; i < contextChunks.Count; i++)
        {
            var chunk = contextChunks[i];
            sb.AppendLine($"[{i + 1}] Quelle: {chunk.FileName} ({chunk.Location})");
            sb.AppendLine(chunk.Text);
            sb.AppendLine();
        }

        sb.AppendLine("Frage:");
        sb.AppendLine(question);

        return sb.ToString();
    }
}
