using Contracts.Tailors;

namespace WorkOrderService.Infrastructure.Messaging;

public interface ITailorProjectionUpdater
{
    Task ApplyAsync(TailorRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
    Task ApplyAsync(TailorDeactivatedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
    Task ApplyAsync(TailorReactivatedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
