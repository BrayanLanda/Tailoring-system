using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Abstractions;

public interface IInvitationTokenGenerator
{
    GeneratedInvitationToken Generate();
}

public sealed record GeneratedInvitationToken(string RawValue, InvitationTokenHash Hash)
{
    public override string ToString() => $"{nameof(GeneratedInvitationToken)} {{ RawValue = [redacted] }}";
}
