using IdentityService.Application.Abstractions;
using IdentityService.Application.TailorInvitations.AcceptTailorInvitation;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Tests;

public sealed class SecretRedactionTests
{
    [Fact]
    public void AcceptCommand_ToString_DoesNotRevealTheTokenOrThePassword()
    {
        var text = new AcceptTailorInvitationCommand(
            Guid.CreateVersion7(), "the-raw-token", "Str0ng-password!").ToString();

        Assert.DoesNotContain("the-raw-token", text);
        Assert.DoesNotContain("Str0ng-password!", text);
    }

    [Fact]
    public void GeneratedToken_ToString_DoesNotRevealTheRawToken()
    {
        var text = new GeneratedInvitationToken("the-raw-token", InvitationTokenHash.Of("hash")).ToString();

        Assert.DoesNotContain("the-raw-token", text);
    }
}
