using System.Text;
using RabbitMQ.Client;

namespace TailorService.Infrastructure.Messaging;

public sealed class RabbitMqIntegrationEventPublisher(RabbitMqOptions options)
    : IIntegrationEventPublisher, IAsyncDisposable
{
    private const string ExchangeName = "sastreria.integration.events";
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(
        string eventType,
        Guid eventId,
        string payload,
        CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

        var properties = new BasicProperties
        {
            MessageId = eventId.ToString(),
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };
        var body = Encoding.UTF8.GetBytes(payload);

        await _channel!.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: eventType,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
                return;

            if (_channel is not null)
                await _channel.DisposeAsync();
            if (_connection is not null)
                await _connection.DisposeAsync();

            var factory = new ConnectionFactory
            {
                HostName = options.Host,
                Port = options.Port,
                UserName = options.Username,
                Password = options.Password,
                AutomaticRecoveryEnabled = true
            };
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);
            _channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken);

            await _channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
        _connectionLock.Dispose();
    }
}
