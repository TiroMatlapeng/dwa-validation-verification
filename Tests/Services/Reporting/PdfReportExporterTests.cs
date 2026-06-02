using dwa_ver_val.Services.Reporting;
using dwa_ver_val.Services.Reporting.Export;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class PdfReportExporterTests
{
    private static ReportTable Sample() => new(
        "Validation Summary",
        new[] { new ReportColumn("Catchment"), new ReportColumn("Volume", true) },
        new[] { (IReadOnlyList<string>)new[] { "A21A", "1000.00" } });

    [Fact]
    public async Task Writes_NonEmptyPdf_WithPdfHeader()
    {
        var sut = new PdfReportExporter();
        using var ms = new MemoryStream();
        await sut.WriteAsync(Sample(), ms, CancellationToken.None);
        var bytes = ms.ToArray();

        Assert.True(bytes.Length > 0);
        // PDF files start with "%PDF"
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    [Fact]
    public void Metadata_IsPdf()
    {
        var sut = new PdfReportExporter();
        Assert.Equal("pdf", sut.Format);
        Assert.Equal("application/pdf", sut.ContentType);
        Assert.Equal(".pdf", sut.FileExtension);
    }
}
