using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace dwa_ver_val.Services.Letters.Templates;

/// <summary>S35(1) — Notice to apply for verification. Must be served in person per S35(2)(d).</summary>
public class S35Letter1Template : ILetterTemplate
{
    public string LetterCode => "S35_L1";
    public string Title => "Notice to apply for verification of existing lawful water use";
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
                "The Department of Water and Sanitation has identified water use on the above property " +
                "that is being investigated as existing lawful water use (ELU) under Section 32 of the " +
                "National Water Act. You are hereby required, in terms of Section 35(1), to apply for " +
                "verification of your existing lawful water use."
            );

            col.Item().Text(
                "Please submit your completed application, together with supporting documentation, to the " +
                "regional office within 60 (sixty) days of the date of this notice."
            );

            if (ctx.DueDate.HasValue)
                col.Item().PaddingTop(6).Text($"Response due by: {ctx.DueDate:dd MMMM yyyy}").Bold();

            col.Item().PaddingTop(6).Text(
                "Failure to respond may result in a Section 53(1) directive compelling compliance. You " +
                "have the right, under Section 35(3)(d), to make written representations during this process."
            );

            if (!string.IsNullOrEmpty(ctx.AdditionalNotes))
                col.Item().PaddingTop(8).Text(ctx.AdditionalNotes!).Italic();

            col.Item().PaddingTop(20).Text(ctx.SignatoryName).Bold();
            col.Item().Text(ctx.SignatoryTitle);
            col.Item().Text(ctx.SignatoryOrgUnit);
        });
    }
}
