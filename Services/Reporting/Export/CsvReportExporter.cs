using System.Text;

namespace dwa_ver_val.Services.Reporting.Export;

public class CsvReportExporter : IReportExporter
{
    public string Format => "csv";
    public string ContentType => "text/csv";
    public string FileExtension => ".csv";

    public async Task WriteAsync(ReportTable table, Stream output, CancellationToken ct)
    {
        // leaveOpen: caller owns the stream (controller wraps it in a FileStreamResult/returns bytes).
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteLineAsync(string.Join(",", table.Columns.Select(c => Quote(c.Header))));
        foreach (var row in table.Rows)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",", row.Select(Quote)));
        }
        await writer.FlushAsync(ct);
    }

    // RFC 4180: quote if the value contains comma, quote, CR or LF; double embedded quotes.
    private static string Quote(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
