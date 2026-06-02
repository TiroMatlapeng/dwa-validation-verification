using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace dwa_ver_val.Services.Reporting.Export;

public class PdfReportExporter : IReportExporter
{
    static PdfReportExporter()
    {
        // Safe to set repeatedly; ensures unit tests (which don't boot Program.cs) can render.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string Format => "pdf";
    public string ContentType => "application/pdf";
    public string FileExtension => ".pdf";

    public Task WriteAsync(ReportTable table, Stream output, CancellationToken ct)
    {
        QuestPDF.Fluent.Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.Header().Text(table.Title).FontSize(16).SemiBold();
                page.Content().PaddingVertical(10).Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        foreach (var _ in table.Columns) cd.RelativeColumn();
                    });
                    foreach (var col in table.Columns)
                        t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(col.Header).SemiBold();
                    foreach (var row in table.Rows)
                        foreach (var val in row)
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(val);
                });
                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Page "); x.CurrentPageNumber(); x.Span(" / "); x.TotalPages();
                });
            });
        }).GeneratePdf(output);
        return Task.CompletedTask;
    }
}
