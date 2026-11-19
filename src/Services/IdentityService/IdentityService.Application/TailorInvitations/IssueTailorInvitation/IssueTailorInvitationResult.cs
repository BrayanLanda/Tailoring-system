using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.TailorInvitations.IssueTailorInvitation;

public sealed record IssueTailorInvitationResult(TailorInvitationId InvitationId, DateTimeOffset ExpiresAt);
