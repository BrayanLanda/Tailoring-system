namespace TailorService.Infrastructure.Messaging;

public sealed record RabbitMqOptions(string Host, int Port, string Username, string Password);
