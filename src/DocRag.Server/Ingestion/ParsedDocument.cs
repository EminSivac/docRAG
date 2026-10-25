namespace DocRag.Server.Ingestion;

public sealed class ParsedSection
{
    public required string Location { get; init; }
    public required string Text { get; init; }
}

public sealed class ParsedDocument
{
    public required string FilePath { get; init; }
    public required List<ParsedSection> Sections { get; init; }
}
