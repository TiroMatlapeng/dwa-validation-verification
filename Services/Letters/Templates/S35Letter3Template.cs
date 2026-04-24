using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace dwa_ver_val.Services.Letters.Templates;

/// <summary>S35(4) — Confirmation of extent and lawfulness of water use (ELU certificate).</summary>
public class S35Letter3Template : ILetterTemplate
{
    public string LetterCode => "S35_L3";
    public string Title => "Confirmation of existing lawful water use";
    public string NWAReference => "Section 35(4) of the National Water Act (Act 38 of 1998)";

    public void Compose(IContainer container, LetterContext ctx)
    {
        container.Column(col =>
        {
            col.Spacing(6);
            col.Item().Text($"To: {ctx.RecipientName}").Bold();
            if (!string.IsNullOrEmpty(ctx.RecipientAddress))
                col.Item().Text(ctx.RecipientAddress!);

            col.Item().PaddingTop(8).Text($"Property: {ctx.FarmName} ({ctx.PropertyReference})");
            col.Item().Text($"V&V Case: {ctx.CaseNumber}");

            col.Item().PaddingTop(10).Text(
                "Following the verification process undertaken in terms of Section 35 of the National " +
                "Water Act, the Department hereby confirms the extent and lawfulness of the existing water " +
                "use on the above property as set out below."
            );

            if (ctx.LawfulVolumeM3.HasValue)
            {
                col.Item().PaddingTop(6).Background("#e8f5e9").Padding(8).Column(inner =>
                {
                    inner.Item().Text("CONFIRMED EXISTING LAWFUL WATER USE").Bold();
                    inner.Item().Text($"{ctx.LawfulVolumeM3:N2} m³ / annum").Bold().FontSize(14);
                });
            }

            if (ctx.UnlawfulVolumeM3.HasValue && ctx.UnlawfulVolumeM3.Value > 0)
            {
                col.Item().PaddingTop(6).Background("#ffebee").Padding(8).Column(inner =>
                {
                    inner.Item().Text("Possible unlawful use identified").Bold();
                    inner.Item().Text($"{ctx.UnlawfulVolumeM3:N2} m³ / annum — a separate notice will follow under Section 53(1).");
                });
            }

            col.Item().PaddingTop(10).Text(
                "This confirmation continues under Section 34 until replaced by a water use licence."
            );

            if (!string.IsNullOrEmpty(ctx.AdditionalNotes))
                col.Item().PaddingTop(8).Text(ctx.AdditionalNotes!).Italic();

            col.Item().PaddingTop(20).Text(ctx.SignatoryName).Bold();
            col.Item().Text(ctx.SignatoryTitle);
            col.Item().Text(ctx.SignatoryOrgUnit);
        });
    }
}
