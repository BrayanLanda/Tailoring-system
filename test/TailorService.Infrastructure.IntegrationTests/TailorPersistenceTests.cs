using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TailorService.Application.Abstractions;
using TailorService.Application.Exceptions;
using TailorService.Application.Tailors.RegisterTailor;
using TailorService.Domain.Entities;
using TailorService.Domain.Repositories;
using TailorService.Domain.ValueObjects;

namespace TailorService.Infrastructure.IntegrationTests;

[Collection(TailorPostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TailorPersistenceTests(TailorPostgresFixture postgres)
{
    [Fact]
    public async Task RegisterTailor_InOneRequest_ReloadsActiveTailorInAnotherRequest()
    {
        await using var writeRequest = postgres.NewRequest();
        var handler = new RegisterTailorHandler(
            writeRequest.ServiceProvider.GetRequiredService<ITailorRepository>(),
            writeRequest.ServiceProvider.GetRequiredService<IUnitOfWork>());

        var result = await handler.HandleAsync(new RegisterTailorCommand("Maria Lopez"));

        await using var readRequest = postgres.NewRequest();
        var reloaded = await readRequest.ServiceProvider
            .GetRequiredService<ITailorRepository>()
            .GetByIdAsync(result.TailorId);

        Assert.NotNull(reloaded);
        Assert.Equal(result.TailorId, reloaded.Id);
        Assert.Equal("Maria Lopez", reloaded.Name);
        Assert.True(reloaded.IsActive);
        Assert.Empty(reloaded.DomainEvents);
    }

    [Fact]
    public async Task RegisterTailor_PersistsIntegrationEventInOutboxWithTailor()
    {
        await using var request = postgres.NewRequest();
        var handler = new RegisterTailorHandler(Repository(request), UnitOfWork(request));

        var result = await handler.HandleAsync(new RegisterTailorCommand("Maria Lopez"));
        var dbContext = request.ServiceProvider.GetRequiredService<TailorService.Infrastructure.Persistence.TailorDbContext>();
        var outboxCount = await dbContext.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM outbox_messages WHERE event_type = 'tailor.registered.v1' AND payload->>'TailorId' = {result.TailorId.Value.ToString()}")
            .SingleAsync();

        Assert.Equal(1, outboxCount);

        var outboxPayload = await dbContext.Database
            .SqlQuery<string>($"SELECT payload::text AS \"Value\" FROM outbox_messages WHERE event_type = 'tailor.registered.v1' AND payload->>'TailorId' = {result.TailorId.Value.ToString()}")
            .SingleAsync();

        Assert.Contains(result.TailorId.Value.ToString(), outboxPayload);
        Assert.Contains("Maria Lopez", outboxPayload);
    }

    [Fact]
    public async Task DeactivateAndReactivateTailor_PersistCorrespondingIntegrationEvents()
    {
        var tailorId = await RegisterTailorAsync();

        await using (var deactivateRequest = postgres.NewRequest())
        {
            var tailor = await Repository(deactivateRequest).GetByIdAsync(tailorId);
            Assert.NotNull(tailor);
            tailor.Deactivate();
            await UnitOfWork(deactivateRequest).SaveChangesAsync();
        }

        await using (var reactivateRequest = postgres.NewRequest())
        {
            var tailor = await Repository(reactivateRequest).GetByIdAsync(tailorId);
            Assert.NotNull(tailor);
            tailor.Reactivate();
            await UnitOfWork(reactivateRequest).SaveChangesAsync();
        }

        await using var verifyRequest = postgres.NewRequest();
        var dbContext = verifyRequest.ServiceProvider.GetRequiredService<TailorService.Infrastructure.Persistence.TailorDbContext>();
        var eventTypes = await dbContext.Database
            .SqlQuery<string>($"SELECT event_type AS \"Value\" FROM outbox_messages WHERE payload->>'TailorId' = {tailorId.Value.ToString()}")
            .ToListAsync();

        Assert.Contains("tailor.registered.v1", eventTypes);
        Assert.Contains("tailor.deactivated.v1", eventTypes);
        Assert.Contains("tailor.reactivated.v1", eventTypes);
    }

    [Fact]
    public async Task TwoConcurrentDeactivations_SecondSaveConflicts()
    {
        var tailorId = await RegisterTailorAsync();

        await using var firstRequest = postgres.NewRequest();
        await using var secondRequest = postgres.NewRequest();
        var tailorA = await Repository(firstRequest).GetByIdAsync(tailorId);
        var tailorB = await Repository(secondRequest).GetByIdAsync(tailorId);
        Assert.NotNull(tailorA);
        Assert.NotNull(tailorB);

        tailorA.Deactivate();
        tailorB.Deactivate();

        await UnitOfWork(firstRequest).SaveChangesAsync();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => UnitOfWork(secondRequest).SaveChangesAsync());

        await using var verifyRequest = postgres.NewRequest();
        var reloaded = await Repository(verifyRequest).GetByIdAsync(tailorId);
        Assert.NotNull(reloaded);
        Assert.False(reloaded.IsActive);
    }

    private async Task<TailorId> RegisterTailorAsync()
    {
        await using var request = postgres.NewRequest();
        var handler = new RegisterTailorHandler(Repository(request), UnitOfWork(request));
        var result = await handler.HandleAsync(new RegisterTailorCommand("Ana Gomez"));
        return result.TailorId;
    }

    private static ITailorRepository Repository(AsyncServiceScope request) =>
        request.ServiceProvider.GetRequiredService<ITailorRepository>();

    private static IUnitOfWork UnitOfWork(AsyncServiceScope request) =>
        request.ServiceProvider.GetRequiredService<IUnitOfWork>();
}
