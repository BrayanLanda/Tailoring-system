using System.Security.Cryptography;
using IdentityService.Application.Abstractions;

namespace IdentityService.Infrastructure.Security;

internal sealed class SecureInvitationTokenGenerator(IInvitationTokenHasher hasher) : IInvitationTokenGenerator
{
    public GeneratedInvitationToken Generate()
    {
        var rawValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return new GeneratedInvitationToken(rawValue, hasher.Hash(rawValue));
    }
}
