using System.Security.Cryptography;
using System.Text;
using IdentityService.Domain.Exceptions;

namespace IdentityService.Domain.ValueObjects;

public sealed record InvitationTokenHash
{
    private InvitationTokenHash(string value) => Value = value;

    public string Value { get; }

    public static InvitationTokenHash Of(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("An invitation token hash is required.");

        return new InvitationTokenHash(value.Trim());
    }

    public bool Matches(InvitationTokenHash other) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Value),
            Encoding.UTF8.GetBytes(other.Value));
}
