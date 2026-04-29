public static class DwsRoles
{
    public const string SystemAdmin = nameof(SystemAdmin);
    public const string NationalManager = nameof(NationalManager);
    public const string RegionalManager = nameof(RegionalManager);
    public const string Validator = nameof(Validator);
    public const string Capturer = nameof(Capturer);
    public const string ReadOnly = nameof(ReadOnly);

    public static readonly string[] All =
    {
        SystemAdmin, NationalManager, RegionalManager, Validator, Capturer, ReadOnly
    };

    // Hierarchies (used by policies — higher-privilege roles satisfy lower-privilege policies)
    public static readonly string[] AtLeastReadOnly = All;
    public static readonly string[] AtLeastCapturer = { SystemAdmin, NationalManager, RegionalManager, Validator, Capturer };
    public static readonly string[] AtLeastValidator = { SystemAdmin, NationalManager, RegionalManager, Validator };
    public static readonly string[] AtLeastRegionalManager = { SystemAdmin, NationalManager, RegionalManager };
    public static readonly string[] AtLeastNationalManager = { SystemAdmin, NationalManager };
    public static readonly string[] AdminOnly = { SystemAdmin };
}
