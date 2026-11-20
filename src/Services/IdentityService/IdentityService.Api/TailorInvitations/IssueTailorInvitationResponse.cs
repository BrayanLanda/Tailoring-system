namespace IdentityService.Api.TailorInvitations;

public sealed record IssueTailorInvitationResponse(Guid InvitationId, DateTimeOffset ExpiresAt);
