using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Tests.Fakes;

internal sealed class FakeInvitationTokenHasher : IInvitationTokenHasher
{
    public InvitationTokenHash Hash(string rawValue) => InvitationTokenHash.Of($"hash-of-{rawValue}");
}
