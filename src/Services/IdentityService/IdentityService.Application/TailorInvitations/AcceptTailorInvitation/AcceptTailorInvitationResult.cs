using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.TailorInvitations.AcceptTailorInvitation
;

public sealed record AcceptTailorInvitationResult(EmailAddress Email);