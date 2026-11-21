using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TailorService.Application.Abstractions;
using TailorService.Application.Tailors.RegisterTailor;
using TailorService.Infrastructure.Messaging;
using TailorService.Infrastructure.Persistence;

namespace TailorService.Infrastructure.IntegrationTests;

[Collection(TailorPostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class OutboxDispatcherTests(TailorPostgresFixture postgres)
{
    [Fact]
    public async Task DispatchPendingAsync_WhenPublisherConfirms_MarksMessageAsProcessed()
    {
        var publisher = new RecordingIntegrationEventPublisher();
        await using var scope = postgres.NewRequest();
        var dbContext = scope.ServiceProvider.GetRequiredService<TailorDbContext>();
        var handler = new RegisterTailorHandler(
            scope.ServiceProvider.GetRequiredService<TailorService.Domain.Repositories.ITailorRepository>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        await handler.HandleAsync(new RegisterTailorCommand("Outbox confirmed"));

        var dispatcher = new OutboxDispatcher(dbContext, publisher);
        var dispatchedCount = await dispatcher.DispatchPendingAsync();

        var eventId = await dbContext.Database
            .SqlQuery<Guid>($"SELECT event_id AS \"Value\" FROM outbox_messages WHERE payload->>'Name' = 'Outbox confirmed'")
            .SingleAsync();
        var message = await dbContext.OutboxMessages.SingleAsync(candidate => candidate.EventId == eventId);
        Assert.Equal(publisher.Messages.Count, dispatchedCount);
        Assert.Contains(publisher.Messages, published =>
            published.EventId == message.EventId &&
            published.EventType == "tailor.registered.v1" &&
            published.Payload.Contains("Outbox confirmed"));
        Assert.NotNull(message.ProcessedOn);
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenPublisherFails_LeavesMessagePendingForRetry()
    {
        await using var scope = postgres.NewRequest();
        var dbContext = scope.ServiceProvider.GetRequiredService<TailorDbContext>();
        var handler = new RegisterTailorHandler(
            scope.ServiceProvider.GetRequiredService<TailorService.Domain.Repositories.ITailorRepository>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        await handler.HandleAsync(new RegisterTailorCommand("Outbox retry"));

        var dispatcher = new OutboxDispatcher(dbContext, new FailingIntegrationEventPublisher());

        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchPendingAsync());

        var eventId = await dbContext.Database
            .SqlQuery<Guid>($"SELECT event_id AS \"Value\" FROM outbox_messages WHERE payload->>'Name' = 'Outbox retry'")
            .SingleAsync();
        var message = await dbContext.OutboxMessages.SingleAsync(candidate => candidate.EventId == eventId);
        Assert.Null(message.ProcessedOn);
    }

    private sealed class RecordingIntegrationEventPublisher : IIntegrationEventPublisher
    {
        public List<(Guid EventId, string EventType, string Payload)> Messages { get; } = [];

        public Task PublishAsync(string eventType, Guid eventId, string payload, CancellationToken cancellationToken)
        {
            Messages.Add((eventId, eventType, payload));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingIntegrationEventPublisher : IIntegrationEventPublisher
    {
        public Task PublishAsync(string eventType, Guid eventId, string payload, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Broker unavailable");
    }
}
