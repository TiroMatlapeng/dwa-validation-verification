namespace dwa_ver_val.Services.Reporting.Export;

/// <summary>Serializes a ReportTable to a stream in one format. Resolved by Format.</summary>
public interface IReportExporter
{
    string Format { get; }        // "csv" | "xlsx" | "pdf"
    string ContentType { get; }
    string FileExtension { get; } // ".csv" etc.
    Task WriteAsync(ReportTable table, Stream output, CancellationToken ct);
}
