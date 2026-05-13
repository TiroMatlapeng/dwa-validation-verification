using Xunit;
using dwa_ver_val.Models.Enums;

namespace dwa_ver_val.Tests.Models;

public class PublicUserPropertyModelTests
{
    [Fact]
    public void NewClaimColumns_HaveExpectedDefaults()
    {
        var pup = new PublicUserProperty
        {
            PublicUserId = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            Status = PropertyClaimStatus.Pending,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = new DateTime(2026, 5, 4)
        };

        Assert.Equal(PropertyClaimStatus.Pending, pup.Status);
        Assert.Equal(PropertyClaimEvidenceType.IdMatch, pup.EvidenceType);
        Assert.Null(pup.EvidenceDocumentId);
        Assert.Equal(new DateTime(2026, 5, 4), pup.RequestedDate);
        Assert.Null(pup.RejectionReason);
    }

    [Fact]
    public void EvidenceDocumentId_AndRejectionReason_AreSettable()
    {
        var docId = Guid.NewGuid();
        var pup = new PublicUserProperty
        {
            PublicUserId = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            Status = PropertyClaimStatus.Rejected,
            EvidenceType = PropertyClaimEvidenceType.TitleDeedUpload,
            EvidenceDocumentId = docId,
            RequestedDate = new DateTime(2026, 5, 4),
            RejectionReason = "Document expired"
        };

        Assert.Equal(docId, pup.EvidenceDocumentId);
        Assert.Equal("Document expired", pup.RejectionReason);
    }
}
