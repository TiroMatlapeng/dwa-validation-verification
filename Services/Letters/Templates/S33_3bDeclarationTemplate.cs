using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace dwa_ver_val.Services.Letters.Templates;

/// <summary>S33(3)(b) — Declaration of ELU on individual application, category B.</summary>
public class S33_3bDeclarationTemplate : ILetterTemplate
{
    public string LetterCode => "S33_3b_Decl";
    public string Title => "Declaration of existing lawful water use — individual application (Category B)";
    public string NWAReference => "Section 33(3)(b) of the National Water Act (Act 38 of 1998)";

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
                "Pursuant to your application made in terms of Section 33(3) of the National Water Act, and " +
                "having considered the supporting evidence submitted, the Department hereby declares the water " +
                "use described in your application, and set out below, to be existing lawful water use under " +
                "Section 33(3)(b) of the Act (Category B)."
            );

            if (ctx.LawfulVolumeM3.HasValue)
            {
                col.Item().PaddingTop(8).Background("#e3f2fd").Padding(8).Column(inner =>
                {
                    inner.Item().Text("DECLARED ELU VOLUME").Bold();
                    inner.Item().Text($"{ctx.LawfulVolumeM3:N2} m³ / annum").Bold().FontSize(14);
                });
            }

            col.Item().PaddingTop(10).Text(
                "This declaration continues under Section 34 of the National Water Act until replaced by a " +
                "water use licence, and is subject to the conditions and limitations of the Act."
            );

            if (!string.IsNullOrEmpty(ctx.AdditionalNotes))
                col.Item().PaddingTop(8).Text(ctx.AdditionalNotes!).Italic();

            col.Item().PaddingTop(20).Text(ctx.SignatoryName).Bold();
            col.Item().Text(ctx.SignatoryTitle);
            col.Item().Text(ctx.SignatoryOrgUnit);
        });
    }
}
