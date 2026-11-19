namespace IdentityService.Application.TailorInvitations.IssueTailorInvitation;

public sealed record IssueTailorInvitationCommand(string Email, Guid TailorProfileId);
