namespace WorkOrderService.Domain.ValueObjects;

public readonly record struct WorkOrderId(Guid Value)
{
    public static WorkOrderId New() => new(Guid.CreateVersion7());
}
