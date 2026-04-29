using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace dwa_ver_val.Services.Letters.Templates;

/// <summary>S35(1) — Directive to provide additional information (issued when Letter 2 is unanswered).</summary>
public class S35Letter2ATemplate : ILetterTemplate
{
    public string LetterCode => "S35_L2A";
    public string Title => "Directive to provide additional information";
    public string NWAReference => "Section 35(1) of the National Water Act (Act 38 of 1998)";

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
                "A request issued in terms of Section 35(3)(a) of the National Water Act required you to " +
                "provide additional information to support the verification of existing lawful water use on the " +
                "above property. The Department has not received the requested information within the response " +
                "period."
            );

            col.Item().PaddingTop(6).Text(
                "You are hereby directed, in terms of Section 35(1) of the National Water Act, to submit the " +
                "outstanding documentation to the regional office without further delay so that the verification " +
                "process can be concluded."
            );

            if (ctx.DueDate.HasValue)
                col.Item().PaddingTop(6).Text($"Compliance required by: {ctx.DueDate:dd MMMM yyyy}").Bold();

            col.Item().PaddingTop(6).Text(
                "Failure to comply with this directive may result in the verification being concluded on the " +
                "information available to the Department, and may attract enforcement action under Section 53 " +
                "and Section 151 of the Act. You retain the right under Section 35(3)(d) to make written " +
                "representations during this process."
            );

            if (!string.IsNullOrEmpty(ctx.AdditionalNotes))
                col.Item().PaddingTop(8).Text(ctx.AdditionalNotes!).Italic();

            col.Item().PaddingTop(20).Text(ctx.SignatoryName).Bold();
            col.Item().Text(ctx.SignatoryTitle);
            col.Item().Text(ctx.SignatoryOrgUnit);
        });
    }
}
