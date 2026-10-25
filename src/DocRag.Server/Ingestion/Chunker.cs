namespace DocRag.Server.Ingestion;

public sealed class TextChunk
{
    public required string Location { get; init; }
    public required int ChunkIndex { get; init; }
    public required string Text { get; init; }
    public required int SectionId { get; init; }
}

/// <summary>
/// Structure-aware chunker: sections (split at headings by the parser) are packed paragraph by paragraph
/// up to the size limit. Oversized paragraphs are split at sentence boundaries. Overlap is carried over
/// as whole sentences, and every chunk is prefixed with its heading path so the embedding keeps context.
/// </summary>
public sealed class Chunker
{
    private static readonly char[] SentenceEnds = { '.', '!', '?', ';' };

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
        var sectionId = 0;
        foreach (var section in document.Sections)
        {
            sectionId++;
            var prefix = string.IsNullOrWhiteSpace(section.Heading) ? "" : $"[{section.Heading}]\n";
            var budget = Math.Max(200, _chunkSizeChars - prefix.Length);

            foreach (var chunkText in ChunkText(section.Text, budget))
            {
                yield return new TextChunk
                {
                    Location = section.Location,
                    ChunkIndex = chunkIndex++,
                    SectionId = sectionId,
                    Text = prefix + chunkText
                };
            }
        }
    }

    private IEnumerable<string> ChunkText(string text, int budget)
    {
        var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        var current = new List<string>();
        var currentLength = 0;

        foreach (var paragraph in paragraphs)
        {
            foreach (var piece in paragraph.Length > budget ? SplitBySentences(paragraph, budget) : new List<string> { paragraph })
            {
                if (currentLength + piece.Length > budget && current.Count > 0)
                {
                    var chunk = string.Join("\n\n", current);
                    yield return chunk;

                    var overlap = TailAtSentenceBoundary(chunk);
                    current = overlap.Length > 0 ? new List<string> { overlap } : new List<string>();
                    currentLength = overlap.Length;
                }

                current.Add(piece);
                currentLength += piece.Length + 2;
            }
        }

        if (current.Count > 0)
        {
            var last = string.Join("\n\n", current);
            if (last.Trim().Length > 0)
            {
                yield return last;
            }
        }
    }

    private List<string> SplitBySentences(string paragraph, int budget)
    {
        var pieces = new List<string>();
        var sb = new System.Text.StringBuilder();

        foreach (var sentence in SplitSentences(paragraph))
        {
            if (sb.Length + sentence.Length > budget && sb.Length > 0)
            {
                pieces.Add(sb.ToString().Trim());
                sb.Clear();
            }

            // A single sentence longer than the budget is cut at word boundaries.
            var rest = sentence;
            while (rest.Length > budget)
            {
                var cut = rest.LastIndexOf(' ', budget - 1);
                if (cut <= 0) cut = budget;
                pieces.Add(rest[..cut].Trim());
                rest = rest[cut..].TrimStart();
            }

            sb.Append(rest);
        }

        if (sb.Length > 0)
        {
            pieces.Add(sb.ToString().Trim());
        }

        return pieces.Where(p => p.Length > 0).ToList();
    }

    private static IEnumerable<string> SplitSentences(string text)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (Array.IndexOf(SentenceEnds, text[i]) >= 0 && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1])))
            {
                yield return text[start..(i + 1)] + (i + 1 < text.Length ? " " : "");
                start = i + 1;
            }
        }

        if (start < text.Length)
        {
            yield return text[start..];
        }
    }

    /// <summary>Last ~overlap characters of the chunk, starting at a sentence (or at least word) boundary.</summary>
    private string TailAtSentenceBoundary(string chunk)
    {
        if (_overlapChars <= 0) return "";
        if (chunk.Length <= _overlapChars) return chunk;

        var tail = chunk[^_overlapChars..];
        var sentenceStart = tail.IndexOfAny(SentenceEnds);
        if (sentenceStart >= 0 && sentenceStart + 1 < tail.Length)
        {
            return tail[(sentenceStart + 1)..].TrimStart();
        }

        var wordStart = tail.IndexOf(' ');
        return wordStart >= 0 ? tail[(wordStart + 1)..] : tail;
    }
}
