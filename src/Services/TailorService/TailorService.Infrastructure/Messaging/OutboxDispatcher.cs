using Microsoft.EntityFrameworkCore;
using TailorService.Infrastructure.Persistence;

namespace TailorService.Infrastructure.Messaging;

public sealed class OutboxDispatcher(
    TailorDbContext dbContext,
    IIntegrationEventPublisher publisher)
{
    private const int BatchSize = 100;

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var messages = await dbContext.OutboxMessages
            .Where(message => message.ProcessedOn == null)
            .OrderBy(message => message.OccurredOn)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var dispatchedCount = 0;
        foreach (var message in messages)
        {
            await publisher.PublishAsync(
                message.EventType,
                message.EventId,
                message.Payload,
                cancellationToken);

            message.MarkAsProcessed(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            dispatchedCount++;
        }

        return dispatchedCount;
    }
}
