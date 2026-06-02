using System.Security.Claims;
using dwa_ver_val.Controllers;
using dwa_ver_val.Services.Reporting;
using dwa_ver_val.Services.Reporting.Export;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace dwa_ver_val.Tests.Controllers;

public class ReportsControllerTests
{
    private sealed class FakeReporting : IReportingService
    {
        public Task<ReportTable> CatchmentProgressAsync(ReportFilter f, ClaimsPrincipal u, CancellationToken ct)
            => Task.FromResult(new ReportTable("Catchment Progress",
                new[] { new ReportColumn("Catchment"), new ReportColumn("Total", true) },
                new[] { (IReadOnlyList<string>)new[] { "A21A", "5" } }));
        public Task<ReportTable> LetterTrackingAsync(ReportFilter f, ClaimsPrincipal u, CancellationToken ct)
            => Task.FromResult(new ReportTable("Letter Tracking", new[] { new ReportColumn("Letter Type") }, Array.Empty<IReadOnlyList<string>>()));
        public Task<ReportTable> ValidationSummaryAsync(ReportFilter f, ClaimsPrincipal u, CancellationToken ct)
            => Task.FromResult(new ReportTable("Validation Summary", new[] { new ReportColumn("Catchment") }, Array.Empty<IReadOnlyList<string>>()));
    }

    private static ReportsController Build()
    {
        var ctrl = new ReportsController(new FakeReporting(),
            new IReportExporter[] { new CsvReportExporter(), new ExcelReportExporter(), new PdfReportExporter() });
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, DwsRoles.NationalManager) }, "Test"))
            }
        };
        return ctrl;
    }

    [Fact]
    public async Task CatchmentProgress_Html_ReturnsViewWithTable()
    {
        var result = await Build().CatchmentProgress(new ReportFilter(), null, CancellationToken.None);
        var view = Assert.IsType<ViewResult>(result);
        var table = Assert.IsType<ReportTable>(view.Model);
        Assert.Equal("Catchment Progress", table.Title);
        Assert.Equal("Report", view.ViewName);
    }

    [Fact]
    public async Task CatchmentProgress_Csv_ReturnsFileResult()
    {
        var result = await Build().CatchmentProgress(new ReportFilter(), "csv", CancellationToken.None);
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", file.ContentType);
        Assert.EndsWith(".csv", file.FileDownloadName);
    }

    [Fact]
    public async Task CatchmentProgress_Xlsx_ReturnsExcelFile()
    {
        var result = await Build().CatchmentProgress(new ReportFilter(), "xlsx", CancellationToken.None);
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
    }

    [Fact]
    public async Task UnknownFormat_FallsBackToHtmlView()
    {
        var result = await Build().CatchmentProgress(new ReportFilter(), "weird", CancellationToken.None);
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task CatchmentProgress_Html_PutsFilterInViewData()
    {
        var filter = new ReportFilter(ValidationStatus: "Completed");
        var result = await Build().CatchmentProgress(filter, null, CancellationToken.None);
        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(filter, view.ViewData["Filter"]);
    }
}
