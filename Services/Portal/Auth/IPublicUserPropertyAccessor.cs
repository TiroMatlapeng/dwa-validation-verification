namespace dwa_ver_val.Services.Portal.Auth;

/// <summary>
/// Single source of truth for row-level scoping in the External Portal.
/// Every portal controller / service that reaches Property / FileMaster /
/// LetterIssuance / Document / Notification data MUST go through this
/// accessor. Direct ApplicationDBContext access from Areas/ExternalPortal
/// is forbidden (enforced by PortalBoundaryTests).
/// </summary>
public interface IPublicUserPropertyAccessor
{
    /// <summary>
    /// Property IDs that the given public user has APPROVED access to.
    /// Pending / Rejected / unknown links return no IDs.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetApprovedPropertyIdsAsync(Guid publicUserId, CancellationToken ct);

    /// <summary>
    /// Throws <see cref="dwa_ver_val.Helpers.NotFoundException"/> if the file
    /// master is not on a property the public user has APPROVED access to.
    /// 404 (not 403) so we don't leak record existence.
    /// </summary>
    Task AssertHasAccessToFileMasterAsync(Guid publicUserId, Guid fileMasterId, CancellationToken ct);
}
