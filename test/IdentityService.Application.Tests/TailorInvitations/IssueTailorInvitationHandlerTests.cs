using IdentityService.Application.TailorInvitations.IssueTailorInvitation;
using IdentityService.Application.Tests.Fakes;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Tests.TailorInvitations;

public sealed class IssueTailorInvitationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(2);

    [Fact]
    public async Task HandleAsync_ValidInvitation_PersistsHashAndDeliversRawToken()
    {
        var repository = new InMemoryTailorInvitationRepository();
        var unitOfWork = new CountingUnitOfWork();
        var tokenGenerator = new FakeInvitationTokenGenerator();
        var delivery = new RecordingInvitationDelivery();
        var handler = new IssueTailorInvitationHandler(
            repository,
            unitOfWork,
            tokenGenerator,
            delivery,
            new FixedTimeProvider(Now),
            InvitationLifetime);
        var tailorProfileId = new TailorProfileId(Guid.CreateVersion7());

        var result = await handler.HandleAsync(
            new IssueTailorInvitationCommand("Ana.Gomez@example.com", tailorProfileId.Value));

        var invitation = Assert.Single(repository.Invitations);
        Assert.Equal("token-hash", invitation.TokenHash.Value);
        Assert.Equal(tailorProfileId, invitation.TailorProfileId);
        Assert.Equal(Now.Add(InvitationLifetime), invitation.ExpiresAt);
        Assert.Equal(invitation.Id, result.InvitationId);
        Assert.Equal(invitation.ExpiresAt, result.ExpiresAt);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Equal(1, tokenGenerator.GenerateCalls);

        var sent = Assert.Single(delivery.Deliveries);
        Assert.Equal("ana.gomez@example.com", sent.Email.Value);
        Assert.Equal("one-time-secret", sent.RawToken);
        Assert.Equal(invitation.Id, sent.InvitationId);
    }

    [Fact]
    public async Task HandleAsync_InvalidEmail_DoesNotGenerateTokenPersistOrDeliver()
    {
        var repository = new InMemoryTailorInvitationRepository();
        var unitOfWork = new CountingUnitOfWork();
        var tokenGenerator = new FakeInvitationTokenGenerator();
        var delivery = new RecordingInvitationDelivery();
        var handler = new IssueTailorInvitationHandler(
            repository,
            unitOfWork,
            tokenGenerator,
            delivery,
            new FixedTimeProvider(Now),
            InvitationLifetime);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new IssueTailorInvitationCommand("invalid-email", Guid.CreateVersion7())));

        Assert.Empty(repository.Invitations);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
        Assert.Equal(0, tokenGenerator.GenerateCalls);
        Assert.Empty(delivery.Deliveries);
    }
}
