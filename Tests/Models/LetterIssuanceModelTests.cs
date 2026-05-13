using Xunit;

namespace dwa_ver_val.Tests.Models;

public class LetterIssuanceModelTests
{
    [Fact]
    public void RecipientPublicUserId_DefaultsToNull_AndIsSettable()
    {
        var letter = new LetterIssuance
        {
            LetterIssuanceId = Guid.NewGuid(),
            FileMasterId = Guid.NewGuid(),
            LetterTypeId = Guid.NewGuid()
        };

        Assert.Null(letter.RecipientPublicUserId);

        var publicUserId = Guid.NewGuid();
        letter.RecipientPublicUserId = publicUserId;
        Assert.Equal(publicUserId, letter.RecipientPublicUserId);
    }
}
