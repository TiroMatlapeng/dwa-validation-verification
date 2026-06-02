using ClosedXML.Excel;
using dwa_ver_val.Services.Reporting;
using dwa_ver_val.Services.Reporting.Export;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class ExcelReportExporterTests
{
    private static ReportTable Sample() => new(
        "Catchment Progress",
        new[] { new ReportColumn("Catchment"), new ReportColumn("Total", true) },
        new[] { (IReadOnlyList<string>)new[] { "A21A", "5" } });

    [Fact]
    public async Task Writes_WorkbookWithHeaderAndRows()
    {
        var sut = new ExcelReportExporter();
        using var ms = new MemoryStream();
        await sut.WriteAsync(Sample(), ms, CancellationToken.None);
        ms.Position = 0;

        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);
        Assert.Equal("Catchment", ws.Cell(1, 1).GetString());
        Assert.Equal("Total", ws.Cell(1, 2).GetString());
        Assert.Equal("A21A", ws.Cell(2, 1).GetString());
        Assert.Equal("5", ws.Cell(2, 2).GetString());
    }

    [Fact]
    public async Task NeutralizesFormulaInjection()
    {
        // ClosedXML strips the leading apostrophe on GetString() (it stores it as the Excel
        // "prefix" quoting convention, which marks the cell as literal text). We therefore
        // verify the cell is NOT a formula and its text content equals the neutralized value.
        var table = new ReportTable("T",
            new[] { new ReportColumn("Name") },
            new[] { (IReadOnlyList<string>)new[] { "=1+1" } });
        var sut = new ExcelReportExporter();
        using var ms = new MemoryStream();
        await sut.WriteAsync(table, ms, CancellationToken.None);
        ms.Position = 0;
        using var wb = new XLWorkbook(ms);
        var cell = wb.Worksheet(1).Cell(2, 1);
        Assert.False(cell.HasFormula);                          // neutralized: not treated as formula
        Assert.Equal(XLDataType.Text, cell.Value.Type);         // stored as literal text
        Assert.Equal("=1+1", cell.GetString());                 // apostrophe was stripped by ClosedXML's prefix convention
    }

    [Fact]
    public void Metadata_IsXlsx()
    {
        var sut = new ExcelReportExporter();
        Assert.Equal("xlsx", sut.Format);
        Assert.Equal(".xlsx", sut.FileExtension);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", sut.ContentType);
    }
}
