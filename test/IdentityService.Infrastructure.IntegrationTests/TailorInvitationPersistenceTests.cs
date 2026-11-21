using IdentityService.Application.Abstractions;
using IdentityService.Application.Exceptions;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Repositories;
using IdentityService.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure.IntegrationTests;

[Collection(IdentityPostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TailorInvitationPersistenceTests(IdentityPostgresFixture postgres)
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 11, 20, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveInOneRequest_ReloadInAnotherRequest_PreservesInvitationAndOnlyItsHash()
    {
        const string rawToken = "token-that-must-not-be-persisted";
        var tailorProfileId = new TailorProfileId(Guid.CreateVersion7());

        await using var writeRequest = postgres.NewRequest();
        var hasher = writeRequest.ServiceProvider.GetRequiredService<IInvitationTokenHasher>();
        var invitation = TailorInvitation.Create(
            EmailAddress.Of("Ana.Gomez@example.com"),
            tailorProfileId,
            hasher.Hash(rawToken),
            IssuedAt,
            IssuedAt.AddDays(2));

        await Repository(writeRequest).AddAsync(invitation);
        await UnitOfWork(writeRequest).SaveChangesAsync();

        await using var readRequest = postgres.NewRequest();
        var reloaded = await Repository(readRequest).GetByIdAsync(invitation.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(invitation.Id, reloaded.Id);
        Assert.Equal("ana.gomez@example.com", reloaded.Email.Value);
        Assert.Equal(tailorProfileId, reloaded.TailorProfileId);
        Assert.Equal(invitation.ExpiresAt, reloaded.ExpiresAt);
        Assert.NotEqual(rawToken, reloaded.TokenHash.Value);
        Assert.True(reloaded.TokenHash.Matches(
            readRequest.ServiceProvider.GetRequiredService<IInvitationTokenHasher>().Hash(rawToken)));
        Assert.Empty(reloaded.DomainEvents);
    }

    [Fact]
    public async Task TwoConcurrentReissues_SecondSaveConflicts()
    {
        var invitationId = await SaveInvitationAsync();

        await using var firstRequest = postgres.NewRequest();
        await using var secondRequest = postgres.NewRequest();
        var first = await Repository(firstRequest).GetByIdAsync(invitationId);
        var second = await Repository(secondRequest).GetByIdAsync(invitationId);
        Assert.NotNull(first);
        Assert.NotNull(second);

        first.Reissue(
            EmailAddress.Of("first@example.com"),
            Hash(firstRequest, "first-token"),
            IssuedAt.AddDays(1),
            IssuedAt.AddDays(3));
        second.Reissue(
            EmailAddress.Of("second@example.com"),
            Hash(secondRequest, "second-token"),
            IssuedAt.AddDays(1),
            IssuedAt.AddDays(3));

        await UnitOfWork(firstRequest).SaveChangesAsync();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => UnitOfWork(secondRequest).SaveChangesAsync());
    }

    [Fact]
    public async Task TwoFirstInvitationsForTheSameProfile_SecondSaveConflicts()
    {
        var profile = new TailorProfileId(Guid.CreateVersion7());
        await using var firstRequest = postgres.NewRequest();
        await using var secondRequest = postgres.NewRequest();

        await Repository(firstRequest).AddAsync(NewInvitation(firstRequest, profile, UniqueEmail()));
        await Repository(secondRequest).AddAsync(NewInvitation(secondRequest, profile, UniqueEmail()));

        await UnitOfWork(firstRequest).SaveChangesAsync();

        // The unique index on tailor_profile_id rejects the second insert.
        // That is a conflict the caller can retry, not a server error.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => UnitOfWork(secondRequest).SaveChangesAsync());
    }

    [Fact]
    public async Task TwoInvitationsForTheSameEmail_SecondSaveConflicts()
    {
        var email = UniqueEmail();
        await using var firstRequest = postgres.NewRequest();
        await using var secondRequest = postgres.NewRequest();

        await Repository(firstRequest).AddAsync(
            NewInvitation(firstRequest, new TailorProfileId(Guid.CreateVersion7()), email));
        await Repository(secondRequest).AddAsync(
            NewInvitation(secondRequest, new TailorProfileId(Guid.CreateVersion7()), email));

        await UnitOfWork(firstRequest).SaveChangesAsync();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => UnitOfWork(secondRequest).SaveChangesAsync());
    }

    private static TailorInvitation NewInvitation(AsyncServiceScope request, TailorProfileId profile, string email) =>
        TailorInvitation.Create(
            EmailAddress.Of(email), profile, Hash(request, "any-token"), IssuedAt, IssuedAt.AddDays(2));

    // Unique emails, because the table has a unique index and the tests share one database.
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    private async Task<TailorInvitationId> SaveInvitationAsync()
    {
        await using var request = postgres.NewRequest();
        var invitation = TailorInvitation.Create(
            EmailAddress.Of("initial@example.com"),
            new TailorProfileId(Guid.CreateVersion7()),
            Hash(request, "initial-token"),
            IssuedAt,
            IssuedAt.AddDays(2));

        await Repository(request).AddAsync(invitation);
        await UnitOfWork(request).SaveChangesAsync();
        return invitation.Id;
    }

    private static InvitationTokenHash Hash(AsyncServiceScope request, string rawToken) =>
        request.ServiceProvider.GetRequiredService<IInvitationTokenHasher>().Hash(rawToken);

    private static ITailorInvitationRepository Repository(AsyncServiceScope request) =>
        request.ServiceProvider.GetRequiredService<ITailorInvitationRepository>();

    private static IUnitOfWork UnitOfWork(AsyncServiceScope request) =>
        request.ServiceProvider.GetRequiredService<IUnitOfWork>();
}
