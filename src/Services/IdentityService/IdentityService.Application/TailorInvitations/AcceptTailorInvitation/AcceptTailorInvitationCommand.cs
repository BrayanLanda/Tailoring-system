namespace IdentityService.Application.TailorInvitations.AcceptTailorInvitation
;

public sealed record AcceptTailorInvitationCommand(Guid InvitationId, string Token, string Password)
{
    public override string ToString() =>
        $"{nameof(AcceptTailorInvitationCommand)} {{ InvitationId = {InvitationId}, Token = [redacted], Password = [redacted] }}";
}