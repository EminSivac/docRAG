using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocRag.Server.Ingestion.Parsers;

public sealed class OfficeParser : IDocumentParser
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".xlsx", ".pptx"
    };

    public bool CanParse(string filePath) => Extensions.Contains(Path.GetExtension(filePath));

    public ParsedDocument Parse(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var sections = ext switch
        {
            ".docx" => ParseWord(filePath),
            ".xlsx" => ParseExcel(filePath),
            ".pptx" => ParsePowerPoint(filePath),
            _ => new List<ParsedSection>()
        };

        return new ParsedDocument { FilePath = filePath, Sections = sections };
    }

    private static List<ParsedSection> ParseWord(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return new List<ParsedSection>();
        }

        var sb = new StringBuilder();
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var text = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
            }
        }

        var fullText = sb.ToString();
        return string.IsNullOrWhiteSpace(fullText)
            ? new List<ParsedSection>()
            : new List<ParsedSection> { new() { Location = "Dokument", Text = fullText } };
    }

    private static List<ParsedSection> ParseExcel(string filePath)
    {
        var sections = new List<ParsedSection>();
        using var doc = SpreadsheetDocument.Open(filePath, false);
        var workbookPart = doc.WorkbookPart;
        if (workbookPart is null)
        {
            return sections;
        }

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

        foreach (var sheet in workbookPart.Workbook!.Descendants<Sheet>())
        {
            if (sheet.Id?.Value is null) continue;
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value!);
            var sb = new StringBuilder();

            foreach (var row in worksheetPart.Worksheet!.Descendants<Row>())
            {
                var cellTexts = new List<string>();
                foreach (var cell in row.Descendants<Cell>())
                {
                    cellTexts.Add(GetCellValue(cell, sharedStrings));
                }

                var rowText = string.Join(" | ", cellTexts.Where(c => !string.IsNullOrWhiteSpace(c)));
                if (!string.IsNullOrWhiteSpace(rowText))
                {
                    sb.AppendLine(rowText);
                }
            }

            var sheetText = sb.ToString();
            if (!string.IsNullOrWhiteSpace(sheetText))
            {
                sections.Add(new ParsedSection { Location = $"Blatt '{sheet.Name}'", Text = sheetText });
            }
        }

        return sections;
    }

    private static string GetCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        var value = cell.CellValue?.InnerText ?? "";
        if (cell.DataType?.Value == CellValues.SharedString && sharedStrings is not null &&
            int.TryParse(value, out var index))
        {
            return sharedStrings.ElementAtOrDefault(index)?.InnerText ?? "";
        }

        return value;
    }

    private static List<ParsedSection> ParsePowerPoint(string filePath)
    {
        var sections = new List<ParsedSection>();
        using var doc = PresentationDocument.Open(filePath, false);
        var presentationPart = doc.PresentationPart;
        if (presentationPart is null)
        {
            return sections;
        }

        var slideIndex = 0;
        foreach (var slidePart in presentationPart.SlideParts)
        {
            slideIndex++;
            var sb = new StringBuilder();
            foreach (var text in slidePart.Slide!.Descendants<DocumentFormat.OpenXml.Drawing.Text>())
            {
                sb.AppendLine(text.Text);
            }

            var slideText = sb.ToString();
            if (!string.IsNullOrWhiteSpace(slideText))
            {
                sections.Add(new ParsedSection { Location = $"Folie {slideIndex}", Text = slideText });
            }
        }

        return sections;
    }
}
