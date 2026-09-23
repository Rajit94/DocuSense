using DocIntel.Core.Entities;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DocIntel.Infrastructure.Processing;

public class PdfExtractor
{
    public record ExtractedPage(int PageNumber, string Text);

    public List<ExtractedPage> Extract(Stream pdfStream)
    {
        var pages = new List<ExtractedPage>();

        using var document = PdfDocument.Open(pdfStream);

        foreach (var page in document.GetPages())
        {
            var text = ExtractTextFromPage(page);

            if (!string.IsNullOrWhiteSpace(text) && text.Length > 20)
            {
                pages.Add(new ExtractedPage(page.Number, text));
            }
        }

        return pages;
    }

    private string ExtractTextFromPage(Page page)
    {
        var words = page.GetWords()
            .OrderByDescending(w => w.BoundingBox.Top)
            .ThenBy(w => w.BoundingBox.Left)
            .Select(w => w.Text);

        return string.Join(" ", words);
    }
}