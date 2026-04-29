using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace dwa_ver_val.Services.Letters.Templates;

/// <summary>S35(3)(a) — Request for additional information to complete verification.</summary>
public class S35Letter2Template : ILetterTemplate
{
    public string LetterCode => "S35_L2";
    public string Title => "Request for additional information";
    public string NWAReference => "Section 35(3)(a) of the National Water Act (Act 38 of 1998)";

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
                "Thank you for your application for verification of existing lawful water use. To complete the " +
                "assessment in terms of Section 35 of the National Water Act, the Department requires the " +
                "additional information set out below."
            );

            col.Item().PaddingTop(6).Text("The Department requests the following supporting documentation:").Bold();
            col.Item().Text("• Title deed(s) for the property, including any water-right notes or servitudes");
            col.Item().Text("• Permits, Section 32 / 33 approvals, transfers, or General Authorisation references relied upon");
            col.Item().Text("• Field-survey evidence, irrigation-board correspondence, or proof of rates paid (where applicable)");
            col.Item().Text("• Any other documentation supporting the extent of water use during the qualifying period (1 October 1996 – 30 September 1998)");

            if (ctx.DueDate.HasValue)
                col.Item().PaddingTop(6).Text($"Information required by: {ctx.DueDate:dd MMMM yyyy}").Bold();

            col.Item().PaddingTop(6).Text(
                "This request is issued under Section 35(3)(a) of the National Water Act. Failure to provide " +
                "the requested information within the response period may result in a Section 35(1) directive. " +
                "You retain the right under Section 35(3)(d) to make written representations during this process."
            );

            if (!string.IsNullOrEmpty(ctx.AdditionalNotes))
                col.Item().PaddingTop(8).Text(ctx.AdditionalNotes!).Italic();

            col.Item().PaddingTop(20).Text(ctx.SignatoryName).Bold();
            col.Item().Text(ctx.SignatoryTitle);
            col.Item().Text(ctx.SignatoryOrgUnit);
        });
    }
}
