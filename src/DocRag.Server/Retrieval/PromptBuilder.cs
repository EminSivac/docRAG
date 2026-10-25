using DocRag.Server.Clients;

namespace DocRag.Server.Retrieval;

public static class PromptBuilder
{
    public const string SystemPrompt =
        "Du bist ein Assistent, der ausschließlich auf Basis der bereitgestellten Dokumentauszüge antwortet. " +
        "Wenn die Antwort nicht in den Auszügen enthalten ist, sag klar, dass du es in den Dokumenten nicht findest. " +
        "Erfinde keine Informationen. Antworte auf Deutsch, prägnant und mit Bezug auf die relevanten Auszüge.";

    public const string PlannerSystemPrompt =
        "Du hilfst bei der Recherche in einer Dokumentensammlung. Zerlege die Frage in 2 bis 4 kurze, " +
        "unterschiedliche Suchanfragen, die zusammen alle Aspekte der Frage abdecken (Synonyme, Teilfragen, Fachbegriffe). " +
        "Gib nur die Suchanfragen aus, eine pro Zeile, ohne Nummerierung und ohne Erklärung.";

    public const string ExtractSystemPrompt =
        "Du bist ein Recherche-Assistent. Du bekommst nummerierte Dokumentauszüge und eine Frage. " +
        "Extrahiere alle Fakten, die zur Beantwortung der Frage relevant sind, als knappe Stichpunkte. " +
        "Setze hinter jeden Stichpunkt die Nummer des Auszugs in eckigen Klammern, z. B. [3]. " +
        "Erfinde nichts. Ist nichts relevant, antworte nur mit: KEINE";

    public const string SynthesisSystemPrompt =
        "Du bist ein gründlicher Recherche-Assistent, der ausschließlich auf Basis der bereitgestellten Notizen aus Dokumenten antwortet. " +
        "Beantworte die Frage vollständig und strukturiert auf Deutsch. Belege Aussagen mit den Quellennummern in eckigen Klammern, z. B. [2]. " +
        "Weise auf Widersprüche zwischen Quellen oder auf Lücken hin. Wenn die Notizen die Frage nicht beantworten, sag das klar. Erfinde keine Informationen.";

    public const string SynthesisSystemPromptDirect =
        "Du bist ein gründlicher Recherche-Assistent, der ausschließlich auf Basis der bereitgestellten Dokumentauszüge antwortet. " +
        "Beantworte die Frage vollständig und strukturiert auf Deutsch. Belege Aussagen mit den Nummern der Auszüge in eckigen Klammern, z. B. [2]. " +
        "Weise auf Widersprüche zwischen Auszügen oder auf Lücken hin. Wenn die Auszüge die Frage nicht beantworten, sag das klar. Erfinde keine Informationen.";

    public static string BuildPlannerPrompt(string question) => $"Frage:\n{question}";

    public static string BuildNumberedExcerpts(string question, IEnumerable<(int Number, QdrantSearchResult Chunk)> items)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Dokumentauszüge:");
        foreach (var (number, chunk) in items)
        {
            sb.AppendLine($"[{number}] Quelle: {chunk.FileName} ({chunk.Location})");
            sb.AppendLine(chunk.Text);
            sb.AppendLine();
        }

        sb.AppendLine("Frage:");
        sb.AppendLine(question);
        return sb.ToString();
    }

    public static string BuildSynthesisPrompt(string question, string notes) =>
        $"Notizen aus den Dokumenten:\n{notes}\n\nFrage:\n{question}";

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
