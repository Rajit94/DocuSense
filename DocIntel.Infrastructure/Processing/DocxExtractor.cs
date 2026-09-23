using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocIntel.Infrastructure.Processing;

public class DocxExtractor
{
    public record ExtractedPage(int PageNumber, string Text);

    public List<ExtractedPage> Extract(Stream docxStream)
    {
        var pages = new List<ExtractedPage>();

        using var document = WordprocessingDocument.Open(docxStream, false);

        var body = document.MainDocumentPart?.Document?.Body;

        if (body is null)
            return pages;

        var paragraphs = body
            .Descendants<Paragraph>()
            .Select(p => p.InnerText.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text) && text.Length > 5)
            .ToList();

        var currentPageText = new System.Text.StringBuilder();
        var pageNumber = 1;
        const int approxPageSize = 3000;

        foreach (var paragraph in paragraphs)
        {
            currentPageText.AppendLine(paragraph);

            if (currentPageText.Length >= approxPageSize)
            {
                pages.Add(new ExtractedPage(pageNumber, currentPageText.ToString()));
                currentPageText.Clear();
                pageNumber++;
            }
        }

        if (currentPageText.Length > 0)
        {
            pages.Add(new ExtractedPage(pageNumber, currentPageText.ToString()));
        }

        return pages;
    }
}