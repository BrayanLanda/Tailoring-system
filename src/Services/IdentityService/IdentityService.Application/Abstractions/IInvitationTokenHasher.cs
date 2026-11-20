using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Abstractions;

public interface IInvitationTokenHasher
{
    InvitationTokenHash Hash(string rawValue);
}