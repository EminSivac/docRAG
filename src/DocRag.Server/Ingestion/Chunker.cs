namespace DocRag.Server.Ingestion;

public sealed class TextChunk
{
    public required string Location { get; init; }
    public required int ChunkIndex { get; init; }
    public required string Text { get; init; }
}

public sealed class Chunker
{
    private readonly int _chunkSizeChars;
    private readonly int _overlapChars;

    public Chunker(int chunkSizeChars, int overlapChars)
    {
        _chunkSizeChars = chunkSizeChars;
        _overlapChars = overlapChars;
    }

    public IEnumerable<TextChunk> ChunkDocument(ParsedDocument document)
    {
        var chunkIndex = 0;
        foreach (var section in document.Sections)
        {
            foreach (var chunkText in ChunkText(section.Text))
            {
                yield return new TextChunk
                {
                    Location = section.Location,
                    ChunkIndex = chunkIndex++,
                    Text = chunkText
                };
            }
        }
    }

    private IEnumerable<string> ChunkText(string text)
    {
        var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (paragraphs.Count == 0)
        {
            paragraphs = new List<string> { text.Trim() };
        }

        var current = new List<string>();
        var currentLength = 0;

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length > _chunkSizeChars)
            {
                if (current.Count > 0)
                {
                    yield return string.Join("\n\n", current);
                    current.Clear();
                    currentLength = 0;
                }

                foreach (var piece in SplitLongParagraph(paragraph))
                {
                    yield return piece;
                }

                continue;
            }

            if (currentLength + paragraph.Length > _chunkSizeChars && current.Count > 0)
            {
                var chunk = string.Join("\n\n", current);
                yield return chunk;

                var overlapText = chunk.Length > _overlapChars
                    ? chunk[^_overlapChars..]
                    : chunk;
                current = new List<string> { overlapText };
                currentLength = overlapText.Length;
            }

            current.Add(paragraph);
            currentLength += paragraph.Length;
        }

        if (current.Count > 0)
        {
            yield return string.Join("\n\n", current);
        }
    }

    private IEnumerable<string> SplitLongParagraph(string paragraph)
    {
        var start = 0;
        while (start < paragraph.Length)
        {
            var length = Math.Min(_chunkSizeChars, paragraph.Length - start);
            yield return paragraph.Substring(start, length);
            start += Math.Max(1, _chunkSizeChars - _overlapChars);
        }
    }
}
