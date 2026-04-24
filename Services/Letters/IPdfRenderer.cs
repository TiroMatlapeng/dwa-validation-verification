using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace dwa_ver_val.Services.Letters;

/// <summary>
/// Thin wrapper over QuestPDF so LetterService can be unit-tested without the library on the hot path.
/// </summary>
public interface IPdfRenderer
{
    byte[] RenderLetter(ILetterTemplate template, LetterContext ctx);
}

public class QuestPdfRenderer : IPdfRenderer
{
    public byte[] RenderLetter(ILetterTemplate template, LetterContext ctx)
    {
        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(11).FontFamily("Helvetica"));

                page.Header().PaddingBottom(10).Column(col =>
                {
                    col.Item().Text("DEPARTMENT OF WATER AND SANITATION").Bold().FontSize(13);
                    col.Item().Text("Validation & Verification of Existing Lawful Water Use").FontSize(9).Italic();
                    col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Blue.Darken3);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().PaddingTop(6).Text(template.Title).Bold().FontSize(14);
                    col.Item().Text(template.NWAReference).Italic().FontSize(10);
                    col.Item().Text($"Reference: {ctx.ReferenceNumber}  |  Issue date: {ctx.IssueDate:dd MMMM yyyy}");
                    col.Item().PaddingBottom(8).LineHorizontal(0.25f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Element(e => template.Compose(e, ctx));
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ").FontSize(9);
                    t.CurrentPageNumber().FontSize(9);
                    t.Span(" of ").FontSize(9);
                    t.TotalPages().FontSize(9);
                });
            });
        }).GeneratePdf();
    }
}
