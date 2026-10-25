using UglyToad.PdfPig;

namespace DocRag.Server.Ingestion.Parsers;

public sealed class PdfParser : IDocumentParser
{
    public bool CanParse(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase);

    public ParsedDocument Parse(string filePath)
    {
        var sections = new List<ParsedSection>();

        using var document = PdfDocument.Open(filePath);
        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sections.Add(new ParsedSection { Location = $"Seite {page.Number}", Text = text });
            }
        }

        return new ParsedDocument { FilePath = filePath, Sections = sections };
    }
}
