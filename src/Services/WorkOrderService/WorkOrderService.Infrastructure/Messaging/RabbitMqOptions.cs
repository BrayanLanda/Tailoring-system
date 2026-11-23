using Microsoft.Extensions.Configuration;

namespace WorkOrderService.Infrastructure.Messaging;

public sealed record RabbitMqOptions(
    string Host,
    int Port,
    string Username,
    string Password,
    int MaxRetries = 3,
    int RetryDelayMilliseconds = 5000)
{
    public static RabbitMqOptions FromConfiguration(IConfiguration configuration)
    {
        var username = configuration["RABBITMQ_USER"]
            ?? throw new InvalidOperationException("Set RABBITMQ_USER before starting WorkOrderService.Api.");
        var password = configuration["RABBITMQ_PASSWORD"]
            ?? throw new InvalidOperationException("Set RABBITMQ_PASSWORD before starting WorkOrderService.Api.");
        var options = new RabbitMqOptions(
            configuration["RABBITMQ_HOST"] ?? "localhost",
            int.TryParse(configuration["RABBITMQ_PORT"], out var port) ? port : 5672,
            username,
            password,
            int.TryParse(configuration["RABBITMQ_MAX_RETRIES"], out var retries) ? retries : 3,
            int.TryParse(configuration["RABBITMQ_RETRY_DELAY_MILLISECONDS"], out var delay) ? delay : 5000);

        if (options.Port is < 1 or > 65535)
            throw new InvalidOperationException("RABBITMQ_PORT must be between 1 and 65535.");
        if (options.MaxRetries < 0)
            throw new InvalidOperationException("RABBITMQ_MAX_RETRIES cannot be negative.");
        if (options.RetryDelayMilliseconds < 1)
            throw new InvalidOperationException("RABBITMQ_RETRY_DELAY_MILLISECONDS must be positive.");

        return options;
    }

    public override string ToString() =>
        $"{nameof(RabbitMqOptions)} {{ Host = {Host}, Port = {Port}, Username = {Username}, Password = [redacted], MaxRetries = {MaxRetries}, RetryDelayMilliseconds = {RetryDelayMilliseconds} }}";
}
