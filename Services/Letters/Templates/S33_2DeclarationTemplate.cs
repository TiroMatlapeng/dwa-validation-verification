using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace dwa_ver_val.Services.Letters.Templates;

/// <summary>S33(2) — Kader Asmal Declaration for irrigation-board scheduled-area water use.</summary>
public class S33_2DeclarationTemplate : ILetterTemplate
{
    public string LetterCode => "S33_2_Decl";
    public string Title => "Declaration of existing lawful water use — scheduled areas";
    public string NWAReference => "Section 33(2) of the National Water Act (Act 38 of 1998) — Kader Asmal Declaration";

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
            if (!string.IsNullOrEmpty(ctx.IrrigationBoardName))
                col.Item().Text($"Irrigation Board: {ctx.IrrigationBoardName}").Bold();

            col.Item().PaddingTop(10).Text(
                "In accordance with Section 33(2) of the National Water Act, and the Kader Asmal Declaration " +
                "published by the Minister, water use on scheduled areas under government and irrigation-board " +
                "schemes as at 1 January 1999, for which the prescribed rates were paid up to 30 September 1998, " +
                "is declared to be existing lawful water use (ELU). This includes dormant but paid-for scheduled " +
                "volumes."
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
                "This declaration continues under Section 34 until replaced by a water use licence, and is " +
                "subject to the limitations of the Act."
            );

            if (!string.IsNullOrEmpty(ctx.AdditionalNotes))
                col.Item().PaddingTop(8).Text(ctx.AdditionalNotes!).Italic();

            col.Item().PaddingTop(20).Text(ctx.SignatoryName).Bold();
            col.Item().Text(ctx.SignatoryTitle);
            col.Item().Text(ctx.SignatoryOrgUnit);
        });
    }
}
