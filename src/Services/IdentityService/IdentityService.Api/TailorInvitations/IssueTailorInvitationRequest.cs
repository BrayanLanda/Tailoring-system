namespace IdentityService.Api.TailorInvitations;

public sealed record IssueTailorInvitationRequest(string Email, Guid TailorProfileId);
