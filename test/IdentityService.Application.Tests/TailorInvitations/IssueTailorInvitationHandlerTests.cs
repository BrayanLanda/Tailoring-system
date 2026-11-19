using IdentityService.Application.Abstractions;
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
        Assert.Equal("token-hash-1", invitation.TokenHash.Value);
        Assert.Equal(tailorProfileId, invitation.TailorProfileId);
        Assert.Equal(Now.Add(InvitationLifetime), invitation.ExpiresAt);
        Assert.Equal(invitation.Id, result.InvitationId);
        Assert.Equal(invitation.ExpiresAt, result.ExpiresAt);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Equal(1, tokenGenerator.GenerateCalls);

        var sent = Assert.Single(delivery.Deliveries);
        Assert.Equal("ana.gomez@example.com", sent.Email.Value);
        Assert.Equal("one-time-secret-1", sent.RawToken);
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

    [Fact]
    public async Task HandleAsync_SecondInvitationForTheSameProfile_ReissuesInsteadOfCreatingAnother()
    {
        var repository = new InMemoryTailorInvitationRepository();
        var delivery = new RecordingInvitationDelivery();
        var handler = CreateHandler(repository, new FakeInvitationTokenGenerator(), delivery);
        var command = new IssueTailorInvitationCommand("ana@example.com", Guid.CreateVersion7());

        var first = await handler.HandleAsync(command);
        var second = await handler.HandleAsync(command);

        var invitation = Assert.Single(repository.Invitations);
        Assert.Equal(first.InvitationId, second.InvitationId);
        Assert.Equal("token-hash-2", invitation.TokenHash.Value);   // the first token no longer matches
        Assert.Equal(2, delivery.Deliveries.Count);
        Assert.Equal("one-time-secret-2", delivery.Deliveries[1].RawToken);
    }

    [Fact]
    public async Task HandleAsync_WhenDeliveryFails_KeepsTheInvitationSoItCanBeReissued()
    {
        var repository = new InMemoryTailorInvitationRepository();
        var tokenGenerator = new FakeInvitationTokenGenerator();
        var command = new IssueTailorInvitationCommand("ana@example.com", Guid.CreateVersion7());

        var failing = CreateHandler(repository, tokenGenerator, new FailingInvitationDelivery());
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.HandleAsync(command));

        Assert.Single(repository.Invitations);
        var delivery = new RecordingInvitationDelivery();
        var working = CreateHandler(repository, tokenGenerator, delivery);

        await working.HandleAsync(command);

        Assert.Single(repository.Invitations);
        var sent = Assert.Single(delivery.Deliveries);
        Assert.Equal("one-time-secret-2", sent.RawToken);
    }

    private static IssueTailorInvitationHandler CreateHandler(
        InMemoryTailorInvitationRepository repository,
        FakeInvitationTokenGenerator tokenGenerator,
        IInvitationDelivery delivery) =>
        new(repository, new CountingUnitOfWork(), tokenGenerator, delivery, new FixedTimeProvider(Now), InvitationLifetime);

    [Fact]
    public async Task HandleAsync_EmptyTailorProfileId_PersistsAndDeliversNothing()
    {
        var repository = new InMemoryTailorInvitationRepository();
        var unitOfWork = new CountingUnitOfWork();
        var delivery = new RecordingInvitationDelivery();
        var handler = new IssueTailorInvitationHandler(
            repository,
            unitOfWork,
            new FakeInvitationTokenGenerator(),
            delivery,
            new FixedTimeProvider(Now),
            InvitationLifetime);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new IssueTailorInvitationCommand("ana@example.com", Guid.Empty)));

        Assert.Empty(repository.Invitations);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
        Assert.Empty(delivery.Deliveries);
    }
}
