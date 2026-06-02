namespace dwa_ver_val.Services.Reporting;

/// <summary>A report column header. Numeric columns are right-aligned and formatted by exporters.</summary>
public record ReportColumn(string Header, bool Numeric = false);

/// <summary>
/// Generic, pre-rendered tabular report result. Rows are already formatted to strings by the
/// ReportingService so exporters and views stay dumb. This is the unit every exporter consumes.
/// </summary>
public record ReportTable(
    string Title,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);
