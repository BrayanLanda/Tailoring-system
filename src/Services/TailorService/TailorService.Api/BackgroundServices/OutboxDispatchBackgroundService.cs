using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TailorService.Infrastructure.Messaging;

namespace TailorService.Api.BackgroundServices;

public sealed class OutboxDispatchBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatchBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                var dispatchedCount = await dispatcher.DispatchPendingAsync(stoppingToken);

                if (dispatchedCount > 0)
                    logger.LogInformation("Dispatched {MessageCount} outbox messages", dispatchedCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to dispatch outbox messages; pending messages will be retried");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
