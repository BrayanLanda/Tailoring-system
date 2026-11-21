namespace TailorService.Infrastructure.Messaging;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(
        string eventType,
        Guid eventId,
        string payload,
        CancellationToken cancellationToken);
}
