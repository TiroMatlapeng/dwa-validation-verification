using dwa_ver_val.Services.Documents;
using dwa_ver_val.Services.Workflow.Guards;
using Xunit;

namespace dwa_ver_val.Tests.Services.Workflow;

public class DocumentRequirementsTests
{
    [Fact]
    public void Map_GatesTitleDeedAndSgAtCp2_AndWarmsAtCp3()
    {
        Assert.Contains(DocumentRequirements.Map["CP2"], r => r.DocumentType == DocumentTypes.TitleDeedReport);
        Assert.Contains(DocumentRequirements.Map["CP2"], r => r.DocumentType == DocumentTypes.SgDiagram);
        Assert.Contains(DocumentRequirements.Map["CP3"], r => r.DocumentType == DocumentTypes.WarmsReport);
    }

    [Fact]
    public void EveryRequiredCode_ExistsInVocabulary()
    {
        foreach (var req in DocumentRequirements.Map.Values.SelectMany(x => x))
            Assert.True(DocumentTypes.IsKnown(req.DocumentType), $"Unknown code: {req.DocumentType}");
        foreach (var req in DocumentRequirements.FileCompilationDocuments)
            Assert.True(DocumentTypes.IsKnown(req.DocumentType), $"Unknown code: {req.DocumentType}");
    }

    [Fact]
    public void FileCompilationDocuments_CoversTheThreeMandatoryItems()
    {
        var codes = DocumentRequirements.FileCompilationDocuments.Select(r => r.DocumentType).ToHashSet();
        Assert.Contains(DocumentTypes.WarmsReport, codes);
        Assert.Contains(DocumentTypes.TitleDeedReport, codes);
        Assert.Contains(DocumentTypes.SgDiagram, codes);
    }

    [Fact]
    public void StatusesFor_MarksPresentAndMissing()
    {
        var present = new HashSet<string> { DocumentTypes.WarmsReport };
        var statuses = DocumentRequirements.StatusesFor(present);

        Assert.Contains(statuses, s => s.DocumentType == DocumentTypes.WarmsReport && s.Mandatory && s.Present);
        Assert.Contains(statuses, s => s.DocumentType == DocumentTypes.SgDiagram && s.Mandatory && !s.Present);
    }
}
