using dwa_ver_val.Services.Reporting;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class ReportTableTests
{
    [Fact]
    public void ReportTable_HoldsTitleColumnsAndRows()
    {
        var table = new ReportTable(
            "Catchment Progress",
            new[] { new ReportColumn("Catchment", false), new ReportColumn("Total", true) },
            new[] { (IReadOnlyList<string>)new[] { "A21A", "5" } });

        Assert.Equal("Catchment Progress", table.Title);
        Assert.Equal(2, table.Columns.Count);
        Assert.True(table.Columns[1].Numeric);
        Assert.Single(table.Rows);
        Assert.Equal("A21A", table.Rows[0][0]);
    }

    [Fact]
    public void ReportFilter_DefaultsArePermissive()
    {
        var f = new ReportFilter();
        Assert.Null(f.DateFrom);
        Assert.Null(f.WaterManagementAreaId);
        Assert.Equal(1, f.Page);
        Assert.Equal(50, f.PageSize);
    }
}
