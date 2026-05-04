using dwa_ver_val.Helpers;
using dwa_ver_val.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Services.Portal.Auth;

public class PublicUserPropertyAccessor : IPublicUserPropertyAccessor
{
    private readonly ApplicationDBContext _db;

    public PublicUserPropertyAccessor(ApplicationDBContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlySet<Guid>> GetApprovedPropertyIdsAsync(
        Guid publicUserId, CancellationToken ct)
    {
        var ids = await _db.PublicUserProperties
            .AsNoTracking()
            .Where(pup => pup.PublicUserId == publicUserId && pup.Status == PropertyClaimStatus.Approved)
            .Select(pup => pup.PropertyId)
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    public async Task AssertHasAccessToFileMasterAsync(
        Guid publicUserId, Guid fileMasterId, CancellationToken ct)
    {
        var hasAccess = await _db.FileMasters
            .AsNoTracking()
            .Where(fm => fm.FileMasterId == fileMasterId)
            .AnyAsync(fm => _db.PublicUserProperties.Any(pup =>
                pup.PublicUserId == publicUserId
                && pup.PropertyId == fm.PropertyId
                && pup.Status == PropertyClaimStatus.Approved), ct);

        if (!hasAccess)
            throw new NotFoundException(
                $"FileMaster {fileMasterId} is not accessible to public user {publicUserId}.");
    }
}
