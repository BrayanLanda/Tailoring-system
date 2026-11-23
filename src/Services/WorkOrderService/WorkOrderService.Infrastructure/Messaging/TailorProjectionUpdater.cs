using Contracts.Tailors;
using Microsoft.EntityFrameworkCore;
using WorkOrderService.Infrastructure.Persistence;
using WorkOrderService.Infrastructure.Persistence.Entities;

namespace WorkOrderService.Infrastructure.Messaging;

public sealed class TailorProjectionUpdater(WorkOrderDbContext dbContext) : ITailorProjectionUpdater
{
    public Task ApplyAsync(TailorRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) =>
        ApplyAsync(integrationEvent.TailorId, true, integrationEvent.OccurredOn, cancellationToken);

    public Task ApplyAsync(TailorDeactivatedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) =>
        ApplyAsync(integrationEvent.TailorId, false, integrationEvent.OccurredOn, cancellationToken);

    public Task ApplyAsync(TailorReactivatedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) =>
        ApplyAsync(integrationEvent.TailorId, true, integrationEvent.OccurredOn, cancellationToken);

    private async Task ApplyAsync(
        Guid tailorId,
        bool isActive,
        DateTimeOffset occurredOn,
        CancellationToken cancellationToken)
    {
        var projection = await dbContext.AssignableTailors
            .SingleOrDefaultAsync(tailor => tailor.TailorId == tailorId, cancellationToken);

        if (projection is null)
        {
            dbContext.AssignableTailors.Add(AssignableTailorProjection.Create(tailorId, isActive, occurredOn));
        }
        else if (!projection.ApplyState(isActive, occurredOn))
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
