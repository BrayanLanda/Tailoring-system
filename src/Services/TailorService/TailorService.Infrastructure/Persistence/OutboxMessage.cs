namespace TailorService.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(Guid eventId, string eventType, string payload, DateTimeOffset occurredOn)
    {
        EventId = eventId;
        EventType = eventType;
        Payload = payload;
        OccurredOn = occurredOn;
    }

    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredOn { get; private set; }
    public DateTimeOffset? ProcessedOn { get; private set; }

    public void MarkAsProcessed(DateTimeOffset processedOn) => ProcessedOn = processedOn;
}
