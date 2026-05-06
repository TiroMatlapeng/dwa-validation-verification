namespace dwa_ver_val.Helpers;

/// <summary>
/// Validates a 13-digit South African ID number against its Luhn checksum.
/// Does NOT validate DOB plausibility, citizenship bit, or ordinal digit —
/// just shape + checksum. Sufficient for portal registration's "is this
/// plausibly a real SA ID?" gate.
/// </summary>
public static class SaIdValidator
{
    public static bool IsValid(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (id.Length != 13) return false;

        var digits = new int[13];
        for (var i = 0; i < 13; i++)
        {
            if (!char.IsDigit(id[i])) return false;
            digits[i] = id[i] - '0';
        }

        // SA ID Luhn: starting from the rightmost digit (the check digit),
        // every second digit is doubled. Sum all digits of the doubled values
        // plus the un-doubled values; the total must be divisible by 10.
        var sum = 0;
        for (var i = 12; i >= 0; i--)
        {
            var d = digits[i];
            if ((12 - i) % 2 == 1)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
        }
        return sum % 10 == 0;
    }
}
