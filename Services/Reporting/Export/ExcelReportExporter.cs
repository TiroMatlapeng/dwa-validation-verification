using ClosedXML.Excel;

namespace dwa_ver_val.Services.Reporting.Export;

public class ExcelReportExporter : IReportExporter
{
    public string Format => "xlsx";
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string FileExtension => ".xlsx";

    public Task WriteAsync(ReportTable table, Stream output, CancellationToken ct)
    {
        using var wb = new XLWorkbook();
        // Excel sheet names: max 31 chars, no : \ / ? * [ ]
        var sheetName = Sanitize(table.Title);
        var ws = wb.Worksheets.Add(sheetName);

        for (var c = 0; c < table.Columns.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = table.Columns[c].Header;
            cell.Style.Font.Bold = true;
        }

        for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Rows[r].Count; c++)
                ws.Cell(r + 2, c + 1).Value = ReportValueGuard.NeutralizeFormula(table.Rows[r][c]);

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        wb.SaveAs(output);
        return Task.CompletedTask;
    }

    private static string Sanitize(string title)
    {
        var clean = new string(title.Where(ch => "\\/:?*[]".IndexOf(ch) < 0).ToArray());
        return clean.Length > 31 ? clean[..31] : (clean.Length == 0 ? "Report" : clean);
    }
}
