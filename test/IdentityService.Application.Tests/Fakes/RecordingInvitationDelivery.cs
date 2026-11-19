using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Tests.Fakes;

internal sealed class RecordingInvitationDelivery : IInvitationDelivery
{
    public List<DeliveredTailorInvitation> Deliveries { get; } = [];

    public Task DeliverAsync(
        EmailAddress email,
        TailorInvitationId invitationId,
        string rawToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        Deliveries.Add(new DeliveredTailorInvitation(email, invitationId, rawToken, expiresAt));
        return Task.CompletedTask;
    }
}

internal sealed record DeliveredTailorInvitation(
    EmailAddress Email,
    TailorInvitationId InvitationId,
    string RawToken,
    DateTimeOffset ExpiresAt);
