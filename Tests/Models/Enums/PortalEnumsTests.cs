using dwa_ver_val.Models.Enums;
using Xunit;

namespace dwa_ver_val.Tests.Models.Enums;

public class PortalEnumsTests
{
    [Fact]
    public void PropertyClaimEvidenceType_HasIDMatchAndTitleDeedUpload()
    {
        Assert.Equal("IDMatch", PropertyClaimEvidenceType.IDMatch.ToString());
        Assert.Equal("TitleDeedUpload", PropertyClaimEvidenceType.TitleDeedUpload.ToString());
    }

    [Fact]
    public void PropertyClaimStatus_HasPendingApprovedRejected()
    {
        Assert.Equal("Pending", PropertyClaimStatus.Pending.ToString());
        Assert.Equal("Approved", PropertyClaimStatus.Approved.ToString());
        Assert.Equal("Rejected", PropertyClaimStatus.Rejected.ToString());
    }
}
