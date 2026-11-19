using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Abstractions;

public interface IInvitationDelivery
{
    Task DeliverAsync(
        EmailAddress email,
        TailorInvitationId invitationId,
        string rawToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);
}
