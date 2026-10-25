using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DocRag.Server.Ingestion.Parsers;

/// <summary>
/// Layout-aware PDF parser: segments each page into text blocks, detects headings by font size/weight,
/// strips repeating headers/footers and returns one section per heading (spanning pages).
/// </summary>
public sealed class PdfParser : IDocumentParser
{
    private const double HeadingSizeFactor = 1.12;
    private const int MaxHeadingChars = 140;
    private const int MaxHeadingLevels = 3;

    private static readonly Regex PageNumberOnly =
        new(@"^\s*(seite|page|s\.)?\s*\d+(\s*(/|von|of)\s*\d+)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed record Block(int Page, string Text, double FontSize, bool Bold, int LineCount);

    public bool CanParse(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase);

    public ParsedDocument Parse(string filePath)
    {
        using var document = PdfDocument.Open(filePath);

        var blocks = new List<Block>();
        foreach (var page in document.GetPages())
        {
            try
            {
                blocks.AddRange(ExtractBlocks(page));
            }
            catch
            {
                // Layout analysis can fail on odd pages; fall back to the raw page text.
                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    blocks.Add(new Block(page.Number, page.Text.Trim(), 0, false, 1));
                }
            }
        }

        blocks = RemoveRunningHeadersAndFooters(blocks, document.NumberOfPages);

        return new ParsedDocument { FilePath = filePath, Sections = BuildSections(blocks) };
    }

    private sealed record Line(string Text, double Top, double Bottom, double FontSize, bool Bold);

    private static IEnumerable<Block> ExtractBlocks(Page page)
    {
        var words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
        if (words.Count == 0)
        {
            yield break;
        }

        // Group words into lines (same baseline), top to bottom.
        var lines = new List<Line>();
        var current = new List<Word>();
        double baseline = 0;
        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
        {
            var height = Math.Max(1, word.BoundingBox.Height);
            if (current.Count > 0 && Math.Abs(word.BoundingBox.Bottom - baseline) > height * 0.5)
            {
                lines.Add(MakeLine(current));
                current = new List<Word>();
            }

            if (current.Count == 0) baseline = word.BoundingBox.Bottom;
            current.Add(word);
        }

        if (current.Count > 0) lines.Add(MakeLine(current));

        // Group lines into paragraph blocks: a larger vertical gap or a font change starts a new block.
        var gaps = lines.Zip(lines.Skip(1), (a, b) => a.Bottom - b.Top).Where(g => g > 0).OrderBy(g => g).ToList();
        var typicalGap = gaps.Count > 0 ? gaps[gaps.Count / 2] : 0;

        var group = new List<Line>();
        foreach (var line in lines)
        {
            if (group.Count > 0)
            {
                var prev = group[^1];
                var gap = prev.Bottom - line.Top;
                var fontChange = Math.Abs(prev.FontSize - line.FontSize) > 0.6 || prev.Bold != line.Bold;
                var bigGap = typicalGap > 0 && gap > typicalGap * 1.6 + 1;
                if (fontChange || bigGap)
                {
                    var block = MakeBlock(page.Number, group);
                    if (block is not null) yield return block;
                    group = new List<Line>();
                }
            }

            group.Add(line);
        }

        if (group.Count > 0)
        {
            var block = MakeBlock(page.Number, group);
            if (block is not null) yield return block;
        }
    }

    private static Line MakeLine(List<Word> words)
    {
        var ordered = words.OrderBy(w => w.BoundingBox.Left).ToList();
        var letters = ordered.SelectMany(w => w.Letters).ToList();
        var bold = letters.Count > 0 && letters.Count(l => l.FontName?.Contains("bold", StringComparison.OrdinalIgnoreCase) == true
            || l.Font?.IsBold == true) > letters.Count / 2;
        return new Line(
            string.Join(" ", ordered.Select(w => w.Text)),
            ordered.Max(w => w.BoundingBox.Top),
            ordered.Min(w => w.BoundingBox.Bottom),
            letters.Count > 0 ? Math.Round(letters.Average(l => l.PointSize), 1) : 0,
            bold);
    }

    private static Block? MakeBlock(int page, List<Line> lines)
    {
        var text = JoinLines(lines.Select(l => l.Text));
        if (text.Length == 0) return null;
        var fontSize = Math.Round(lines.Average(l => l.FontSize), 1);
        return new Block(page, text, fontSize, lines.Count(l => l.Bold) > lines.Count / 2, lines.Count);
    }

    private static string JoinLines(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (sb.Length > 0)
            {
                // Re-join words that were hyphenated at a line break ("Verar-" + "beitung").
                if (sb[^1] == '-' && sb.Length > 1 && char.IsLetter(sb[^2]) && char.IsLower(line[0]))
                {
                    sb.Length--;
                }
                else
                {
                    sb.Append(' ');
                }
            }

            sb.Append(line);
        }

        return sb.ToString();
    }

    private static List<Block> RemoveRunningHeadersAndFooters(List<Block> blocks, int pageCount)
    {
        var repeated = new HashSet<string>();
        if (pageCount >= 4)
        {
            var threshold = Math.Max(3, (int)(pageCount * 0.4));
            repeated = blocks
                .Where(b => b.Text.Length <= 150)
                .GroupBy(b => Regex.Replace(b.Text, @"\d+", "#"))
                .Where(g => g.Select(b => b.Page).Distinct().Count() >= threshold)
                .Select(g => g.Key)
                .ToHashSet();
        }

        return blocks
            .Where(b => !PageNumberOnly.IsMatch(b.Text))
            .Where(b => b.Text.Length > 150 || !repeated.Contains(Regex.Replace(b.Text, @"\d+", "#")))
            .ToList();
    }

    private static List<ParsedSection> BuildSections(List<Block> blocks)
    {
        var bodySize = DetectBodyFontSize(blocks);
        var headingSizes = blocks
            .Where(b => IsHeading(b, bodySize))
            .Select(b => b.FontSize)
            .Distinct()
            .OrderByDescending(s => s)
            .Take(MaxHeadingLevels)
            .ToList();

        var sections = new List<ParsedSection>();
        var path = new string?[MaxHeadingLevels];
        var body = new List<string>();
        var firstPage = 0;
        var lastPage = 0;
        string? currentHeading = null;

        void Flush()
        {
            if (body.Count > 0)
            {
                var location = firstPage == lastPage ? $"Seite {firstPage}" : $"Seite {firstPage}–{lastPage}";
                sections.Add(new ParsedSection
                {
                    Location = location,
                    Heading = currentHeading,
                    Text = string.Join("\n\n", body)
                });
            }

            body = new List<string>();
        }

        foreach (var block in blocks)
        {
            if (IsHeading(block, bodySize))
            {
                var level = headingSizes.IndexOf(block.FontSize);
                if (level < 0) level = headingSizes.Count > 0 ? headingSizes.Count - 1 : 0;
                level = Math.Min(level, MaxHeadingLevels - 1);

                Flush();
                path[level] = block.Text;
                for (var i = level + 1; i < MaxHeadingLevels; i++) path[i] = null;
                currentHeading = string.Join(" > ", path.Where(p => p is not null));
                firstPage = lastPage = block.Page;
                continue;
            }

            if (body.Count == 0)
            {
                firstPage = block.Page;
            }

            lastPage = block.Page;
            body.Add(block.Text);
        }

        Flush();
        return sections;
    }

    private static double DetectBodyFontSize(List<Block> blocks)
    {
        // The most common font size, weighted by amount of text, is the body text.
        return blocks
            .Where(b => b.FontSize > 0)
            .GroupBy(b => b.FontSize)
            .OrderByDescending(g => g.Sum(b => b.Text.Length))
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    private static bool IsHeading(Block block, double bodySize)
    {
        if (block.FontSize <= 0 || bodySize <= 0) return false;
        if (block.Text.Length > MaxHeadingChars || block.LineCount > 2) return false;
        if (block.Text.EndsWith('.') && block.Text.Split(' ').Length > 12) return false;

        var larger = block.FontSize >= bodySize * HeadingSizeFactor;
        var boldTitle = block.Bold && block.FontSize >= bodySize * 0.98 && block.Text.Length <= 90
                        && !block.Text.EndsWith('.');
        return larger || boldTitle;
    }
}
