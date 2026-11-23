using System.Text;
using System.Text.Json;
using Contracts.Tailors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace WorkOrderService.Infrastructure.Messaging;

public sealed class TailorEventConsumer(
    RabbitMqOptions options,
    IServiceScopeFactory scopeFactory,
    ILogger<TailorEventConsumer> logger)
{
    public const string ExchangeName = "sastreria.integration.events";
    public const string QueueName = "work-order.tailor-events";
    public const string DeadLetterQueueName = "work-order.tailor-events.dlq";

    private const string RetryExchangeName = "work-order.tailor-events.retry.exchange";
    private const string RetryQueueName = "work-order.tailor-events.retry";
    private const string DeadLetterExchangeName = "work-order.tailor-events.dlx";
    private const string DeadLetterRoutingKey = "work-order.tailor-events.failed";
    private const string RetryCountHeader = "x-retry-count";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default) =>
        _ready.Task.WaitAsync(timeout, cancellationToken);

    public async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = options.Host,
                Port = options.Port,
                UserName = options.Username,
                Password = options.Password,
                AutomaticRecoveryEnabled = true
            };

            await using var connection = await factory.CreateConnectionAsync(stoppingToken);
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);
            await using var channel = await connection.CreateChannelAsync(channelOptions, stoppingToken);

            await DeclareTopologyAsync(channel, stoppingToken);
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, eventArgs) => ProcessMessageAsync(channel, eventArgs, stoppingToken);
            await channel.BasicConsumeAsync(
                QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            logger.LogInformation("Consuming TailorService events from RabbitMQ queue {QueueName}.", QueueName);
            _ready.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _ready.TrySetCanceled(stoppingToken);
        }
        catch (Exception exception)
        {
            _ready.TrySetException(exception);
            logger.LogError(exception, "Tailor event consumer failed while connecting or processing its queue.");
            throw;
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            RetryExchangeName, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            DeadLetterExchangeName, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);

        var retryArguments = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = options.RetryDelayMilliseconds,
            ["x-dead-letter-exchange"] = ExchangeName
        };
        await channel.QueueDeclareAsync(
            RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: retryArguments,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            RetryQueueName, RetryExchangeName, "tailor.#", cancellationToken: cancellationToken);

        var mainQueueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchangeName,
            ["x-dead-letter-routing-key"] = DeadLetterRoutingKey
        };
        await channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(QueueName, ExchangeName, "tailor.#", cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            DeadLetterQueueName, DeadLetterExchangeName, DeadLetterRoutingKey, cancellationToken: cancellationToken);
    }

    private async Task ProcessMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        try
        {
            await ApplyEventAsync(eventArgs, cancellationToken);
            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Invalid JSON in Tailor event {MessageId}; sending to dead-letter queue.", eventArgs.BasicProperties.MessageId);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, cancellationToken);
        }
        catch (NotSupportedException exception)
        {
            logger.LogError(exception, "Unsupported Tailor event {RoutingKey}; sending to dead-letter queue.", eventArgs.RoutingKey);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            var retryCount = ReadRetryCount(eventArgs.BasicProperties.Headers);
            if (retryCount >= options.MaxRetries)
            {
                logger.LogError(exception, "Tailor event {MessageId} failed after {RetryCount} retries; sending to dead-letter queue.", eventArgs.BasicProperties.MessageId, retryCount);
                await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                return;
            }

            try
            {
                var retryProperties = new BasicProperties
                {
                    MessageId = eventArgs.BasicProperties.MessageId,
                    ContentType = eventArgs.BasicProperties.ContentType,
                    DeliveryMode = DeliveryModes.Persistent,
                    Headers = new Dictionary<string, object?>
                    {
                        [RetryCountHeader] = retryCount + 1
                    }
                };

                await channel.BasicPublishAsync(
                    RetryExchangeName,
                    eventArgs.RoutingKey,
                    mandatory: true,
                    basicProperties: retryProperties,
                    body: eventArgs.Body,
                    cancellationToken: cancellationToken);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
            }
            catch (Exception publishException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(publishException, "Could not schedule retry for Tailor event {MessageId}; requeuing original.", eventArgs.BasicProperties.MessageId);
                await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken);
            }
        }
    }

    private async Task ApplyEventAsync(BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var updater = scope.ServiceProvider.GetRequiredService<ITailorProjectionUpdater>();
        var payload = eventArgs.Body;

        switch (eventArgs.RoutingKey)
        {
            case "tailor.registered.v1":
                await updater.ApplyAsync(
                    JsonSerializer.Deserialize<TailorRegisteredIntegrationEvent>(payload.Span, SerializerOptions)
                    ?? throw new JsonException("Tailor registration event payload was empty."),
                    cancellationToken);
                break;
            case "tailor.deactivated.v1":
                await updater.ApplyAsync(
                    JsonSerializer.Deserialize<TailorDeactivatedIntegrationEvent>(payload.Span, SerializerOptions)
                    ?? throw new JsonException("Tailor deactivation event payload was empty."),
                    cancellationToken);
                break;
            case "tailor.reactivated.v1":
                await updater.ApplyAsync(
                    JsonSerializer.Deserialize<TailorReactivatedIntegrationEvent>(payload.Span, SerializerOptions)
                    ?? throw new JsonException("Tailor reactivation event payload was empty."),
                    cancellationToken);
                break;
            default:
                throw new NotSupportedException($"Unsupported Tailor event routing key '{eventArgs.RoutingKey}'.");
        }
    }

    private static int ReadRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(RetryCountHeader, out var value))
            return 0;

        return value switch
        {
            byte number => number,
            int number => number,
            long number => checked((int)number),
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            _ => 0
        };
    }
}
