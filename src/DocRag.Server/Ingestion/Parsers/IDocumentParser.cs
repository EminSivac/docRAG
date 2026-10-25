namespace DocRag.Server.Ingestion.Parsers;

public interface IDocumentParser
{
    bool CanParse(string filePath);
    ParsedDocument Parse(string filePath);
}
