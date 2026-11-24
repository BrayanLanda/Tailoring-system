using System.Text.Json;
using Contracts.Tailors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using TailorService.Application.Abstractions;
using TailorService.Application.Tailors.RegisterTailor;
using TailorService.Infrastructure;
using TailorService.Infrastructure.Messaging;
using TailorService.Infrastructure.Persistence;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Tailors;
using WorkOrderService.Infrastructure.Messaging;

namespace WorkOrderService.Infrastructure.IntegrationTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TailorOutboxToProjectionFlowTests(PostgresFixture workOrderPostgres)
{
    [Fact]
    public async Task RegisterTailor_DispatchOutbox_PersistsAssignableTailorProjection()
    {
        const string username = "sastreria-flow-test";
        const string password = "rabbit-flow-test-password";

        await using var tailorPostgres = new PostgreSqlBuilder("postgres:17").Build();
        await using var rabbit = new RabbitMqBuilder("rabbitmq:4-management")
            .WithUsername(username)
            .WithPassword(password)
            .Build();
        await Task.WhenAll(tailorPostgres.StartAsync(), rabbit.StartAsync());

        await using var tailorProvider = new ServiceCollection()
            .AddTailorInfrastructure(tailorPostgres.GetConnectionString())
            .BuildServiceProvider();

        await using (var migrationScope = tailorProvider.CreateAsyncScope())
        {
            var tailorDb = migrationScope.ServiceProvider.GetRequiredService<TailorDbContext>();
            await tailorDb.Database.MigrateAsync();
        }

        var brokerUri = new Uri(rabbit.GetConnectionString());
        var rabbitUsername = Uri.UnescapeDataString(brokerUri.UserInfo.Split(':')[0]);
        var rabbitPassword = Uri.UnescapeDataString(brokerUri.UserInfo.Split(':')[1]);
        var consumer = new TailorEventConsumer(
            new WorkOrderService.Infrastructure.Messaging.RabbitMqOptions(
                brokerUri.Host, brokerUri.Port, rabbitUsername, rabbitPassword, RetryDelayMilliseconds: 100),
            workOrderPostgres.ScopeFactory,
            NullLogger<TailorEventConsumer>.Instance);
        using var cancellation = new CancellationTokenSource();
        var consuming = consumer.ConsumeAsync(cancellation.Token);
        await consumer.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));

        var tailorOptions = new TailorService.Infrastructure.Messaging.RabbitMqOptions(
            brokerUri.Host, brokerUri.Port, rabbitUsername, rabbitPassword);
        await using var publisher = new RabbitMqIntegrationEventPublisher(tailorOptions);

        await using (var request = tailorProvider.CreateAsyncScope())
        {
            var services = request.ServiceProvider;
            var register = new RegisterTailorHandler(
                services.GetRequiredService<TailorService.Domain.Repositories.ITailorRepository>(),
                services.GetRequiredService<TailorService.Application.Abstractions.IUnitOfWork>());
            var result = await register.HandleAsync(new RegisterTailorCommand("Flow tailor"));
            var outboxMessage = await services.GetRequiredService<TailorDbContext>()
                .OutboxMessages.SingleAsync(message => message.ProcessedOn == null);
            var expectedEvent = JsonSerializer.Deserialize<TailorRegisteredIntegrationEvent>(outboxMessage.Payload);
            Assert.NotNull(expectedEvent);

            var dispatcher = new OutboxDispatcher(
                services.GetRequiredService<TailorDbContext>(),
                publisher);
            Assert.Equal(1, await dispatcher.DispatchPendingAsync());

            var projection = await WaitForProjectionAsync(result.TailorId.Value);
            Assert.NotNull(projection);
            Assert.True(projection.IsActive);
            var postgresPrecisionOccurredOn = new DateTimeOffset(
                expectedEvent.OccurredOn.Ticks / 10 * 10,
                expectedEvent.OccurredOn.Offset);
            Assert.Equal(postgresPrecisionOccurredOn, projection.LastOccurredOn);
        }

        cancellation.Cancel();
        await consuming;
    }

    private async Task<AssignableTailor?> WaitForProjectionAsync(Guid tailorId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var request = workOrderPostgres.NewRequest();
            var repository = request.ServiceProvider.GetRequiredService<IAssignableTailorRepository>();
            var projection = await repository.GetByIdAsync(tailorId);
            if (projection is not null)
                return projection;

            await Task.Delay(100);
        }

        return null;
    }
}
