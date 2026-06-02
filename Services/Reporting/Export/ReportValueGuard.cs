namespace dwa_ver_val.Services.Reporting.Export;

/// <summary>
/// Neutralizes spreadsheet formula injection: a cell value beginning with = + - @ (or
/// tab/CR/LF) is prefixed with an apostrophe so Excel/Sheets treat it as literal text.
/// </summary>
internal static class ReportValueGuard
{
    public static string NeutralizeFormula(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var c = value[0];
        if (c is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n')
            return "'" + value;
        return value;
    }
}
