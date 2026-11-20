namespace IdentityService.Application.Tests.Fakes;

internal sealed class FakeInvitationTokenHasher : IInvitationTokenHasher
{
    public InvitationTokenHash Hash(string rawValue) => InvitationTokenHash.Of($"hash-of-{rawValue}");
}
