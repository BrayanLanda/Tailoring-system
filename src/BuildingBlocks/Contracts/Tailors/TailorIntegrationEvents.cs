namespace Contracts.Tailors;

public sealed record TailorRegisteredIntegrationEvent(
    Guid EventId,
    Guid TailorId,
    string Name,
    DateTimeOffset OccurredOn);

public sealed record TailorDeactivatedIntegrationEvent(
    Guid EventId,
    Guid TailorId,
    DateTimeOffset OccurredOn);

public sealed record TailorReactivatedIntegrationEvent(
    Guid EventId,
    Guid TailorId,
    DateTimeOffset OccurredOn);
