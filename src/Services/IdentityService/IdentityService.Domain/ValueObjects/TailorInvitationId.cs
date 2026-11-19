namespace IdentityService.Domain.ValueObjects;

public readonly record struct TailorInvitationId(Guid Value)
{
    public static TailorInvitationId New() => new(Guid.CreateVersion7());
}
