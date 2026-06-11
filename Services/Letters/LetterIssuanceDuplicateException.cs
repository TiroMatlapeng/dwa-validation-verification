namespace dwa_ver_val.Services.Letters;

/// <summary>
/// Thrown by <see cref="LetterService"/> when a concurrent issuance attempt for the
/// SAME letter type on the SAME case hits the filtered unique index on
/// (FileMasterId, LetterTypeId) WHERE ReissuedFromId IS NULL.
/// </summary>
/// <remarks>
/// The filtered index allows at most one ORIGINAL issuance per (case, letter type).
/// Legitimate re-issuances (where <c>ReissuedFromId</c> is set) are exempt from the
/// index and are never blocked.
///
/// Callers (controllers, API endpoints) should surface this to the operator as
/// "This letter has already been issued for this case." so they understand the
/// request was idempotent-rejected, not lost.
/// </remarks>
public sealed class LetterIssuanceDuplicateException : InvalidOperationException
{
    public LetterIssuanceDuplicateException()
        : base("This letter has already been issued for this case.")
    {
    }

    public LetterIssuanceDuplicateException(string message)
        : base(message)
    {
    }

    public LetterIssuanceDuplicateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
