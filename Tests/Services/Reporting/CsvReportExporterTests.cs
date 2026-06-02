using System.Text;
using dwa_ver_val.Services.Reporting;
using dwa_ver_val.Services.Reporting.Export;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class CsvReportExporterTests
{
    private static ReportTable Sample() => new(
        "Letter Tracking",
        new[] { new ReportColumn("Type"), new ReportColumn("Issued", true) },
        new[]
        {
            (IReadOnlyList<string>)new[] { "S35_L1", "3" },
            (IReadOnlyList<string>)new[] { "Has, comma", "1" },
        });

    [Fact]
    public async Task Writes_HeaderAndRows_WithRfc4180Quoting()
    {
        var sut = new CsvReportExporter();
        using var ms = new MemoryStream();
        await sut.WriteAsync(Sample(), ms, CancellationToken.None);
        var csv = Encoding.UTF8.GetString(ms.ToArray());

        Assert.Contains("Type,Issued", csv);
        Assert.Contains("S35_L1,3", csv);
        Assert.Contains("\"Has, comma\",1", csv); // comma value is quoted
    }

    [Fact]
    public void Metadata_IsCsv()
    {
        var sut = new CsvReportExporter();
        Assert.Equal("csv", sut.Format);
        Assert.Equal("text/csv", sut.ContentType);
        Assert.Equal(".csv", sut.FileExtension);
    }
}
