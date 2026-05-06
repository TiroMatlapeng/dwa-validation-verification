namespace dwa_ver_val.Helpers;

/// <summary>
/// Stage 2a portal password policy: ≥12 chars, at least one letter, at least
/// one digit. No symbol requirement.
/// </summary>
public static class PortalPasswordPolicy
{
    public const int MinimumLength = 12;

    public static IReadOnlyList<string> Validate(string? password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required.");
            return errors;
        }

        if (password.Length < MinimumLength)
            errors.Add($"Password must be at least {MinimumLength} characters long.");

        if (!password.Any(char.IsLetter))
            errors.Add("Password must contain at least one letter.");

        if (!password.Any(char.IsDigit))
            errors.Add("Password must contain at least one digit.");

        return errors;
    }
}
