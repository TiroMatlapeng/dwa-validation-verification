using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace dwa_ver_val.Services.Letters.Templates;

/// <summary>S53(1) — Directive to apply for verification (issued to non-responsive Letter 1 recipients).</summary>
public class S35Letter1ATemplate : ILetterTemplate
{
    public string LetterCode => "S35_L1A";
    public string Title => "Directive to apply for verification of existing lawful water use";
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
                "A notice issued in terms of Section 35(1) of the National Water Act required you to apply " +
                "for verification of existing lawful water use on the above property. The Department has not " +
                "received your application within the statutory response period."
            );

            col.Item().PaddingTop(6).Text(
                "You are hereby directed, in terms of Section 53(1) of the National Water Act, to submit your " +
                "completed application together with all required supporting documentation to the regional " +
                "office without further delay."
            );

            if (ctx.DueDate.HasValue)
                col.Item().PaddingTop(6).Text($"Compliance required by: {ctx.DueDate:dd MMMM yyyy}").Bold();

            col.Item().PaddingTop(6).Text(
                "Failure to comply with this directive constitutes an offence under Section 151 of the National " +
                "Water Act and may result in further enforcement action, including criminal prosecution. You " +
                "retain the right under Section 35(3)(d) to make written representations during this process."
            );

            if (!string.IsNullOrEmpty(ctx.AdditionalNotes))
                col.Item().PaddingTop(8).Text(ctx.AdditionalNotes!).Italic();

            col.Item().PaddingTop(20).Text(ctx.SignatoryName).Bold();
            col.Item().Text(ctx.SignatoryTitle);
            col.Item().Text(ctx.SignatoryOrgUnit);
        });
    }
}
