using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Tests.Fakes;

public class FailingInvitationDelivery : IInvitationDelivery
{
    public Task DeliverAsync(EmailAddress email, TailorInvitationId invitationId, string rawToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) => Task.FromException(new InvalidOperationException("The email service is down."));
}
