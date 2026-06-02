namespace dwa_ver_val.Services.Documents;

/// <summary>
/// Controlled vocabulary of document type codes used by both the internal and external
/// uploaders and by the workflow document-requirements map. Codes are persisted to
/// Document.DocumentType. Appendix A items are flagged for report/panel grouping.
/// </summary>
public static class DocumentTypes
{
    public const string WarmsReport     = "WARMSReport";      // Appendix A item 2
    public const string TitleDeedReport = "TitleDeedReport";  // Appendix A item 3
    public const string SgDiagram       = "SGDiagram";        // Appendix A item 4
    public const string PreviousStudy   = "PreviousStudy";    // Appendix A item 5 (optional)
    public const string TitleDeed       = "TitleDeed";
    public const string Permit          = "Permit";
    public const string FieldSurvey     = "FieldSurvey";
    public const string Correspondence  = "Correspondence";
    public const string Other           = "Other";

    public static readonly IReadOnlyDictionary<string, (string Display, bool IsAppendixA)> All =
        new Dictionary<string, (string, bool)>(StringComparer.Ordinal)
        {
            [WarmsReport]     = ("WARMS Report", true),
            [TitleDeedReport] = ("Title Deed Report", true),
            [SgDiagram]       = ("SG Diagram", true),
            [PreviousStudy]   = ("Previous Study / Legislative Docs", true),
            [TitleDeed]       = ("Title Deed", false),
            [Permit]          = ("Permit", false),
            [FieldSurvey]     = ("Field Survey", false),
            [Correspondence]  = ("Correspondence", false),
            [Other]           = ("Other", false),
        };

    public static bool IsKnown(string? code) => code is not null && All.ContainsKey(code);

    public static string Display(string code) =>
        All.TryGetValue(code, out var v) ? v.Display : code;
}
