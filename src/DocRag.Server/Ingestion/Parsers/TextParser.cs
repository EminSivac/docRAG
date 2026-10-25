namespace DocRag.Server.Ingestion.Parsers;

public sealed class TextParser : IDocumentParser
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md"
    };

    public bool CanParse(string filePath) => Extensions.Contains(Path.GetExtension(filePath));

    public ParsedDocument Parse(string filePath)
    {
        var text = File.ReadAllText(filePath);
        var sections = string.IsNullOrWhiteSpace(text)
            ? new List<ParsedSection>()
            : new List<ParsedSection> { new() { Location = "Dokument", Text = text } };

        return new ParsedDocument { FilePath = filePath, Sections = sections };
    }
}
