using dwa_ver_val.Services.Documents;

namespace dwa_ver_val.Services.Workflow.Guards;

/// <summary>One required document for a control point.</summary>
public record DocReq(string DocumentType, string DisplayName, string AtControlPoint);

/// <summary>Row used by the FileMaster documents panel checklist.</summary>
public record DocumentRequirementStatus(
    string DocumentType, string DisplayName, bool Mandatory, string? MandatoryAtCp, bool Present);

/// <summary>
/// Single source of truth for which documents are mandatory at which control point.
/// Keyed by control-point PREFIX (matches Cp2SpatialInfoGuard.IsLeaving). Consumed by
/// DocumentEvidenceGuard (per-CP gating) and Cp11FileCompilationGuard (final re-check).
/// </summary>
public static class DocumentRequirements
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<DocReq>> Map =
        new Dictionary<string, IReadOnlyList<DocReq>>(StringComparer.OrdinalIgnoreCase)
        {
            ["CP2"] = new[]
            {
                new DocReq(DocumentTypes.TitleDeedReport, "Title Deed report", "CP2"),
                new DocReq(DocumentTypes.SgDiagram, "SG Diagram", "CP2"),
            },
            ["CP3"] = new[]
            {
                new DocReq(DocumentTypes.WarmsReport, "WARMS report", "CP3"),
            },
        };

    /// <summary>The full Appendix A document set the CP11 compilation guard must re-check.</summary>
    public static readonly IReadOnlyList<DocReq> FileCompilationDocuments =
        Map.Values.SelectMany(x => x).ToList();

    /// <summary>
    /// Builds the panel checklist: every mandatory document (from the map), each marked
    /// present/missing against the supplied set of document-type codes already on the case.
    /// </summary>
    public static IReadOnlyList<DocumentRequirementStatus> StatusesFor(ISet<string> presentTypes)
    {
        return FileCompilationDocuments
            .GroupBy(r => r.DocumentType)
            .Select(g =>
            {
                var first = g.First();
                return new DocumentRequirementStatus(
                    first.DocumentType,
                    first.DisplayName,
                    Mandatory: true,
                    MandatoryAtCp: first.AtControlPoint,
                    Present: presentTypes.Contains(first.DocumentType));
            })
            .ToList();
    }
}
