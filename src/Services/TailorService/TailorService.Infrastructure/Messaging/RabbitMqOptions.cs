namespace TailorService.Infrastructure.Messaging;

public sealed record RabbitMqOptions(string Host, int Port, string Username, string Password)
{
    // A record prints every property. The password must never reach a log.
    public override string ToString() =>
        $"{nameof(RabbitMqOptions)} {{ Host = {Host}, Port = {Port}, Username = {Username}, Password = [redacted] }}";
}
