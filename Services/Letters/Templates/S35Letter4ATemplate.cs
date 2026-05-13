using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace dwa_ver_val.Services.Letters.Templates;

/// <summary>S53(1) — Pre-directive notice of intent to issue a directive to stop unlawful water use.</summary>
public class S35Letter4ATemplate : ILetterTemplate
{
    public string LetterCode => "S35_L4A";
    public string Title => "Notice of intent to issue directive to stop unlawful water use";
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
                "Following the verification process undertaken in terms of Section 35 of the National Water " +
                "Act, the Department has determined that a portion of the water use on the above property is " +
                "not covered by existing lawful water use (ELU) and therefore appears to be unlawful."
            );

            if (ctx.UnlawfulVolumeM3.HasValue && ctx.UnlawfulVolumeM3.Value > 0)
            {
                col.Item().PaddingTop(8).Background("#fff8e1").Padding(8).Column(inner =>
                {
                    inner.Item().Text("POSSIBLE UNLAWFUL USE IDENTIFIED").Bold();
                    inner.Item().Text($"{ctx.UnlawfulVolumeM3:N2} m³ / annum").Bold().FontSize(14);
                });
            }

            col.Item().PaddingTop(8).Text(
                "This is a notice of the Department's intention to issue a directive in terms of Section 53(1) " +
                "of the National Water Act compelling you to stop the unlawful portion of the water use. Before " +
                "any such directive is issued, you are afforded an opportunity, in accordance with Section " +
                "35(3)(d) of the Act and the Promotion of Administrative Justice Act, 2000, to make written " +
                "representations setting out any factual or legal grounds upon which you contend that the use is " +
                "lawful."
            );

            if (ctx.DueDate.HasValue)
                col.Item().PaddingTop(6).Text($"Representations to be submitted by: {ctx.DueDate:dd MMMM yyyy}").Bold();

            col.Item().PaddingTop(6).Text(
                "Should no representations be received, or should the representations not displace the " +
                "Department's findings, a directive to stop the unlawful use will follow under Section 53(1)."
            );

            if (!string.IsNullOrEmpty(ctx.AdditionalNotes))
                col.Item().PaddingTop(8).Text(ctx.AdditionalNotes!).Italic();

            col.Item().PaddingTop(20).Text(ctx.SignatoryName).Bold();
            col.Item().Text(ctx.SignatoryTitle);
            col.Item().Text(ctx.SignatoryOrgUnit);
        });
    }
}
