using System.Text;
using dwa_ver_val.Services.Reporting;
using dwa_ver_val.Services.Reporting.Export;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

/// <summary>
/// RPT-01: verifies NeutralizeFormula behaviour through the public CsvReportExporter surface.
/// Negative numbers must NOT be corrupted; injection vectors must still be neutralized.
/// </summary>
public class ReportValueGuardTests
{
    private static async Task<string> ExportSingleCell(string cellValue)
    {
        var table = new ReportTable("T",
            new[] { new ReportColumn("V") },
            new[] { (IReadOnlyList<string>)new[] { cellValue } });
        using var ms = new MemoryStream();
        await new CsvReportExporter().WriteAsync(table, ms, CancellationToken.None);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // ── valid numbers pass through unchanged ─────────────────────────────────

    [Theory]
    [InlineData("-5")]
    [InlineData("-1234.50")]
    [InlineData("-0.01")]
    [InlineData("3.14")]
    public async Task NeutralizeFormula_ValidNumber_PassesThroughUnchanged(string value)
    {
        var csv = await ExportSingleCell(value);
        // The value should appear verbatim in the CSV (no leading apostrophe).
        Assert.Contains(value, csv);
        Assert.DoesNotContain("'" + value, csv);
    }

    // ── injection vectors are neutralized ────────────────────────────────────

    [Theory]
    [InlineData("=cmd|'/c calc'!A1")]
    [InlineData("+x")]
    [InlineData("-cmd")]
    [InlineData("@SUM(A1)")]
    public async Task NeutralizeFormula_InjectionVector_GetsLeadingApostrophe(string input)
    {
        var csv = await ExportSingleCell(input);
        Assert.Contains("'" + input, csv);
    }
}
