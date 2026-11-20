using System.Security.Cryptography;
using System.Text;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Infrastructure.Security;

internal sealed class Sha256InvitationTokenHasher : IInvitationTokenHasher
{
    public InvitationTokenHash Hash(string rawValue)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return InvitationTokenHash.Of(Convert.ToHexString(bytes));
    }
}
