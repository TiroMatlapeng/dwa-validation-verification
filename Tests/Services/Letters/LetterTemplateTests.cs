using System.Text.Json;
using dwa_ver_val.Services.Letters;
using dwa_ver_val.Services.Letters.Templates;
using QuestPDF.Infrastructure;
using Xunit;

namespace dwa_ver_val.Tests.Services.Letters;

public class LetterTemplateTests
{
    static LetterTemplateTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static LetterContext LoadFixture()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "contracts", "fixtures", "letters", "letter-context.json"));
        using var s = File.OpenRead(path);
        var doc = JsonDocument.Parse(s);
        var r = doc.RootElement;
        return new LetterContext(
            ReferenceNumber: r.GetProperty("referenceNumber").GetString()!,
            IssueDate: DateOnly.Parse(r.GetProperty("issueDate").GetString()!),
            DueDate: r.TryGetProperty("dueDate", out var d) && d.ValueKind != JsonValueKind.Null ? DateOnly.Parse(d.GetString()!) : null,
            CaseNumber: r.GetProperty("caseNumber").GetString()!,
            FarmName: r.GetProperty("farmName").GetString()!,
            PropertyReference: r.GetProperty("propertyReference").GetString()!,
            RecipientName: r.GetProperty("recipientName").GetString()!,
            RecipientAddress: r.TryGetProperty("recipientAddress", out var a) && a.ValueKind != JsonValueKind.Null ? a.GetString() : null,
            IrrigationBoardName: r.TryGetProperty("irrigationBoardName", out var ib) && ib.ValueKind != JsonValueKind.Null ? ib.GetString() : null,
            SignatoryName: r.GetProperty("signatoryName").GetString()!,
            SignatoryTitle: r.GetProperty("signatoryTitle").GetString()!,
            SignatoryOrgUnit: r.GetProperty("signatoryOrgUnit").GetString()!,
            LawfulVolumeM3: r.TryGetProperty("lawfulVolumeM3", out var lv) && lv.ValueKind != JsonValueKind.Null ? lv.GetDecimal() : null,
            UnlawfulVolumeM3: r.TryGetProperty("unlawfulVolumeM3", out var uv) && uv.ValueKind != JsonValueKind.Null ? uv.GetDecimal() : null,
            AdditionalNotes: r.TryGetProperty("additionalNotes", out var an) && an.ValueKind != JsonValueKind.Null ? an.GetString() : null);
    }

    [Fact]
    public void S35Letter1_Renders_Fixture_ToNonEmptyPdf()
    {
        var ctx = LoadFixture();
        var bytes = new QuestPdfRenderer().RenderLetter(new S35Letter1Template(), ctx);
        Assert.NotEmpty(bytes);
        // PDF magic: "%PDF"
        Assert.True(bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46);
    }

    [Fact]
    public void S35Letter3_Renders_WithLawfulVolume_ProducesPdf()
    {
        var ctx = LoadFixture() with { LawfulVolumeM3 = 12345.67m };
        var bytes = new QuestPdfRenderer().RenderLetter(new S35Letter3Template(), ctx);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void S33_2Declaration_Renders_WithIrrigationBoard_ProducesPdf()
    {
        var ctx = LoadFixture() with { IrrigationBoardName = "Blyde River Irrigation Board", LawfulVolumeM3 = 99000m };
        var bytes = new QuestPdfRenderer().RenderLetter(new S33_2DeclarationTemplate(), ctx);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void TemplateRegistry_ResolvesByCode()
    {
        var reg = new LetterTemplateRegistry(new ILetterTemplate[]
        {
            new S35Letter1Template(),
            new S35Letter3Template(),
            new S33_2DeclarationTemplate()
        });
        Assert.Equal("S35_L1", reg.Get("S35_L1").LetterCode);
        Assert.Equal("S35_L3", reg.Get("s35_l3").LetterCode);
        Assert.Equal("S33_2_Decl", reg.Get("S33_2_Decl").LetterCode);
        Assert.Throws<InvalidOperationException>(() => reg.Get("Nonsense"));
    }
}
