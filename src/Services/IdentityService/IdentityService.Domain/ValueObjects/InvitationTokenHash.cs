using IdentityService.Domain.Exceptions;

namespace IdentityService.Domain.ValueObjects;

public readonly record struct InvitationTokenHash
{
    private InvitationTokenHash(string value) => Value = value;

    public string Value { get; }

    public static InvitationTokenHash Of(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("An invitation token hash is required.");

        return new InvitationTokenHash(value.Trim());
    }
}
