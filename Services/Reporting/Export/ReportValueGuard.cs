using System.Globalization;

namespace dwa_ver_val.Services.Reporting.Export;

/// <summary>
/// Neutralizes spreadsheet formula injection: a cell value beginning with = + - @ (or
/// tab/CR/LF) is prefixed with an apostrophe so Excel/Sheets treat it as literal text.
/// Exception: valid numeric strings (e.g. negative numbers like "-5") are passed through
/// unchanged — they are not injection vectors and should remain numeric in the spreadsheet.
/// </summary>
internal static class ReportValueGuard
{
    public static string NeutralizeFormula(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var c = value[0];
        if (c is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n')
        {
            // Do not neutralize a plain number — "-5" or "-1234.50" is data, not a formula.
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                return value;
            return "'" + value;
        }
        return value;
    }
}
