using System.Text.Json;
using Contracts.Tailors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Tailors;
using WorkOrderService.Infrastructure;
using WorkOrderService.Infrastructure.Messaging;

namespace WorkOrderService.Infrastructure.IntegrationTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TailorEventConsumerTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Consume_PersistsEventAndMovesRepeatedFailuresToDeadLetterQueue()
    {
        const string username = "sastreria-test";
        const string password = "rabbit-test-password";
        const int maxRetries = 2;

        await using var rabbit = new RabbitMqBuilder("rabbitmq:4-management")
            .WithUsername(username)
            .WithPassword(password)
            .Build();
        await rabbit.StartAsync();

        var brokerUri = new Uri(rabbit.GetConnectionString());
        var options = new RabbitMqOptions(
            brokerUri.Host,
            brokerUri.Port,
            Uri.UnescapeDataString(brokerUri.UserInfo.Split(':')[0]),
            Uri.UnescapeDataString(brokerUri.UserInfo.Split(':')[1]),
            maxRetries,
            250);

        var consumer = new TailorEventConsumer(
            options,
            postgres.ScopeFactory,
            NullLogger<TailorEventConsumer>.Instance);
        using var healthyCancellation = new CancellationTokenSource();
        var healthyConsumerTask = consumer.ConsumeAsync(healthyCancellation.Token);
        await consumer.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));

        var factory = new ConnectionFactory
        {
            HostName = brokerUri.Host,
            Port = brokerUri.Port,
            UserName = username,
            Password = password
        };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var occurredOn = DateTimeOffset.UtcNow;
        occurredOn = new DateTimeOffset(occurredOn.Ticks / 10 * 10, TimeSpan.Zero);
        var registration = new TailorRegisteredIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Integrated tailor", occurredOn);
        await PublishAsync(channel, "tailor.registered.v1", registration);

        var projection = await WaitForProjectionAsync(registration.TailorId);
        Assert.NotNull(projection);
        Assert.True(projection.IsActive);
        Assert.Equal(registration.OccurredOn, projection.LastOccurredOn);

        await channel.QueueDeclarePassiveAsync("work-order.tailor-events");
        await channel.QueueDeclarePassiveAsync("work-order.tailor-events.dlq");
        healthyCancellation.Cancel();
        await healthyConsumerTask;

        var failingUpdater = new FailingTailorProjectionUpdater();
        await using var failingProvider = new ServiceCollection()
            .AddLogging()
            .AddWorkOrderInfrastructure(postgres.ConnectionString)
            .ReplaceTailorProjectionUpdater(failingUpdater)
            .BuildServiceProvider();

        var failingConsumer = new TailorEventConsumer(
            options,
            failingProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TailorEventConsumer>.Instance);
        using var failingCancellation = new CancellationTokenSource();
        var failingConsumerTask = failingConsumer.ConsumeAsync(failingCancellation.Token);
        await failingConsumer.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));

        var poisonEvent = new TailorDeactivatedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        await PublishAsync(channel, "tailor.deactivated.v1", poisonEvent);

        var deadLetter = await WaitForDeadLetterAsync(channel);
        Assert.NotNull(deadLetter);
        Assert.Equal(maxRetries + 1, failingUpdater.Attempts);
        await channel.BasicAckAsync(deadLetter.DeliveryTag, multiple: false);

        failingCancellation.Cancel();
        await failingConsumerTask;
    }

    private async Task<AssignableTailor?> WaitForProjectionAsync(Guid tailorId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var request = postgres.NewRequest();
            var repository = request.ServiceProvider.GetRequiredService<IAssignableTailorRepository>();
            var projection = await repository.GetByIdAsync(tailorId);
            if (projection is not null)
                return projection;

            await Task.Delay(100);
        }

        return null;
    }

    private static async Task<BasicGetResult?> WaitForDeadLetterAsync(IChannel channel)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var message = await channel.BasicGetAsync("work-order.tailor-events.dlq", autoAck: false);
            if (message is not null)
                return message;

            await Task.Delay(100);
        }

        return null;
    }

    private static async Task PublishAsync<TEvent>(IChannel channel, string routingKey, TEvent integrationEvent)
    {
        var properties = new BasicProperties
        {
            MessageId = integrationEvent switch
            {
                TailorRegisteredIntegrationEvent registered => registered.EventId.ToString(),
                TailorDeactivatedIntegrationEvent deactivated => deactivated.EventId.ToString(),
                TailorReactivatedIntegrationEvent reactivated => reactivated.EventId.ToString(),
                _ => Guid.NewGuid().ToString()
            },
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await channel.BasicPublishAsync(
            "sastreria.integration.events",
            routingKey,
            mandatory: true,
            properties,
            JsonSerializer.SerializeToUtf8Bytes(integrationEvent));
    }

    private sealed class FailingTailorProjectionUpdater : ITailorProjectionUpdater
    {
        public int Attempts { get; private set; }

        public Task ApplyAsync(TailorRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) => Fail();
        public Task ApplyAsync(TailorDeactivatedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) => Fail();
        public Task ApplyAsync(TailorReactivatedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) => Fail();

        private Task Fail()
        {
            Attempts++;
            throw new InvalidOperationException("Simulated projection failure.");
        }
    }
}

internal static class TestServiceCollectionExtensions
{
    public static IServiceCollection ReplaceTailorProjectionUpdater(
        this IServiceCollection services,
        ITailorProjectionUpdater updater)
    {
        services.RemoveAll<ITailorProjectionUpdater>();
        services.AddSingleton(updater);
        return services;
    }
}
