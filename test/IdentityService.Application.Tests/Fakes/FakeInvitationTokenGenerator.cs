using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Tests.Fakes;

internal sealed class FakeInvitationTokenGenerator : IInvitationTokenGenerator
{
    public int GenerateCalls { get; private set; }

    public GeneratedInvitationToken Generate()
    {
        GenerateCalls++;
        return new GeneratedInvitationToken("one-time-secret", InvitationTokenHash.Of("token-hash"));
    }
}
