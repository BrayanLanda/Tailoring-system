using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Api;

public class DevelopmentInvitationDelivery(ILogger<DevelopmentInvitationDelivery> logger) : IInvitationDelivery
{
    public Task DeliverAsync(EmailAddress email, TailorInvitationId invitationId, string rawToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "DEVELOPMENT ONLY. Invitation for {Email}: invitationId={InvitationId} token={Token} expiresAt={ExpiresAt:u}",
            email.Value, invitationId.Value, rawToken, expiresAt);

        return Task.CompletedTask;
    }
}
