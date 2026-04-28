using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace dwa_ver_val.Services.Letters.Templates;

/// <summary>S53(1) — Final directive to stop unlawful water use after representations considered.</summary>
public class S35Letter4_5Template : ILetterTemplate
{
    public string LetterCode => "S35_L4_5";
    public string Title => "Directive to stop unlawful water use";
    public string NWAReference => "Section 53(1) of the National Water Act (Act 38 of 1998)";

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
                "The Department previously notified you of its intention to issue a directive under Section " +
                "53(1) of the National Water Act in respect of unlawful water use on the above property. The " +
                "Department has considered the representations received (if any), and the determination that " +
                "the use is unlawful is upheld."
            );

            if (ctx.UnlawfulVolumeM3.HasValue && ctx.UnlawfulVolumeM3.Value > 0)
            {
                col.Item().PaddingTop(8).Background("#ffebee").Padding(8).Column(inner =>
                {
                    inner.Item().Text("DIRECTIVE TO STOP — UNLAWFUL VOLUME").Bold();
                    inner.Item().Text($"{ctx.UnlawfulVolumeM3:N2} m³ / annum").Bold().FontSize(14);
                });
            }

            col.Item().PaddingTop(8).Text(
                "You are hereby directed, in terms of Section 53(1) of the National Water Act, to cease the " +
                "unlawful portion of the water use on the property forthwith. Any continuation of the unlawful " +
                "use is now subject to enforcement under Section 151 of the Act, including criminal prosecution " +
                "and the civil remedies available to the Department."
            );

            if (ctx.DueDate.HasValue)
                col.Item().PaddingTop(6).Text($"Cessation required by: {ctx.DueDate:dd MMMM yyyy}").Bold();

            col.Item().PaddingTop(6).Text(
                "Should you wish to legalise the cessation of unlawful use through a water use licence " +
                "application or other authorisation, please contact the regional office. This directive does " +
                "not affect the lawful portion of your water use, which continues under Section 34 of the Act."
            );

            if (!string.IsNullOrEmpty(ctx.AdditionalNotes))
                col.Item().PaddingTop(8).Text(ctx.AdditionalNotes!).Italic();

            col.Item().PaddingTop(20).Text(ctx.SignatoryName).Bold();
            col.Item().Text(ctx.SignatoryTitle);
            col.Item().Text(ctx.SignatoryOrgUnit);
        });
    }
}
