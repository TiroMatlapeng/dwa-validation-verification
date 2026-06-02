using dwa_ver_val.Services.Documents;
using Xunit;

namespace dwa_ver_val.Tests.Services.Documents;

public class DocumentTypesTests
{
    [Fact]
    public void All_ContainsAppendixADocumentsFlaggedTrue()
    {
        Assert.True(DocumentTypes.All[DocumentTypes.WarmsReport].IsAppendixA);
        Assert.True(DocumentTypes.All[DocumentTypes.TitleDeedReport].IsAppendixA);
        Assert.True(DocumentTypes.All[DocumentTypes.SgDiagram].IsAppendixA);
    }

    [Fact]
    public void IsKnown_TrueForVocabularyCode_FalseForJunk()
    {
        Assert.True(DocumentTypes.IsKnown(DocumentTypes.WarmsReport));
        Assert.False(DocumentTypes.IsKnown("NotARealType"));
    }

    [Fact]
    public void Display_ReturnsHumanLabel()
    {
        Assert.Equal("WARMS Report", DocumentTypes.Display(DocumentTypes.WarmsReport));
    }
}
