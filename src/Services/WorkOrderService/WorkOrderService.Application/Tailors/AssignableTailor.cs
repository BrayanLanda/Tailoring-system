namespace WorkOrderService.Application.Tailors;

public sealed record AssignableTailor(Guid TailorId, bool IsActive, DateTimeOffset LastOccurredOn);
