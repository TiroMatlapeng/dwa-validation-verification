using QuestPDF.Infrastructure;

namespace dwa_ver_val.Services.Letters;

/// <summary>
/// One implementation per letter type code (S35_L1, S35_L3, S33_2_Decl, etc.).
/// Consumes LetterContext — never null fields without explicit contract exemption.
/// </summary>
public interface ILetterTemplate
{
    /// <summary>Letter type code — matches <see cref="LetterType.LetterName"/> in the DB and the registry lookup.</summary>
    string LetterCode { get; }

    /// <summary>Human-readable title for the page header.</summary>
    string Title { get; }

    /// <summary>Statutory reference (e.g. "Section 35(1) of the National Water Act").</summary>
    string NWAReference { get; }

    /// <summary>Writes the body of the letter onto the given QuestPDF container.</summary>
    void Compose(IContainer container, LetterContext ctx);
}

public interface ILetterTemplateRegistry
{
    ILetterTemplate Get(string letterCode);
    IEnumerable<ILetterTemplate> All();
}

public class LetterTemplateRegistry : ILetterTemplateRegistry
{
    private readonly Dictionary<string, ILetterTemplate> _byCode;
    public LetterTemplateRegistry(IEnumerable<ILetterTemplate> templates)
    {
        _byCode = templates.ToDictionary(t => t.LetterCode, StringComparer.OrdinalIgnoreCase);
    }

    public ILetterTemplate Get(string letterCode) =>
        _byCode.TryGetValue(letterCode, out var t)
            ? t
            : throw new InvalidOperationException($"No letter template registered for code '{letterCode}'.");

    public IEnumerable<ILetterTemplate> All() => _byCode.Values;
}
