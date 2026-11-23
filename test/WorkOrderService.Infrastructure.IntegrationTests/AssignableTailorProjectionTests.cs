using Contracts.Tailors;
using Microsoft.Extensions.DependencyInjection;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Infrastructure.Messaging;

namespace WorkOrderService.Infrastructure.IntegrationTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AssignableTailorProjectionTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ApplyAsync_TracksLatestTailorStateAndIgnoresDuplicateOrOlderEvents()
    {
        var tailorId = Guid.NewGuid();
        var firstEventAt = DateTimeOffset.Parse("2026-11-23T12:00:00Z");
        var deactivatedAt = firstEventAt.AddMinutes(2);
        var reactivatedAt = firstEventAt.AddMinutes(3);

        await using var request = postgres.NewRequest();
        var updater = request.ServiceProvider.GetRequiredService<TailorProjectionUpdater>();
        var repository = request.ServiceProvider.GetRequiredService<IAssignableTailorRepository>();

        await updater.ApplyAsync(new TailorRegisteredIntegrationEvent(
            Guid.NewGuid(), tailorId, "Maria Lopez", firstEventAt));
        await updater.ApplyAsync(new TailorDeactivatedIntegrationEvent(
            Guid.NewGuid(), tailorId, deactivatedAt));
        await updater.ApplyAsync(new TailorDeactivatedIntegrationEvent(
            Guid.NewGuid(), tailorId, deactivatedAt));
        await updater.ApplyAsync(new TailorReactivatedIntegrationEvent(
            Guid.NewGuid(), tailorId, firstEventAt.AddMinutes(1)));

        var inactive = await repository.GetByIdAsync(tailorId);
        Assert.NotNull(inactive);
        Assert.False(inactive.IsActive);
        Assert.Equal(deactivatedAt, inactive.LastOccurredOn);

        await updater.ApplyAsync(new TailorReactivatedIntegrationEvent(
            Guid.NewGuid(), tailorId, reactivatedAt));

        var active = await repository.GetByIdAsync(tailorId);
        Assert.NotNull(active);
        Assert.True(active.IsActive);
        Assert.Equal(reactivatedAt, active.LastOccurredOn);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTailorHasNoEvents_ReturnsNull()
    {
        await using var request = postgres.NewRequest();
        var repository = request.ServiceProvider.GetRequiredService<IAssignableTailorRepository>();

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
