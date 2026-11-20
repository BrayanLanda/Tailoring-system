namespace IdentityService.Api.TailorInvitations;

public sealed record AcceptTailorInvitationRequest(Guid InvitationId, string Token, string Password)
{
    public override string ToString() =>
        $"{nameof(AcceptTailorInvitationRequest)} {{ InvitationId = {InvitationId}, Token = [redacted], Password = [redacted] }}";
}
