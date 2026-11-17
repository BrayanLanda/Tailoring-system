namespace WorkOrderService.Domain.ValueObjects;

public readonly record struct PaymentId(Guid Value)
{
    public static PaymentId New() => new(Guid.CreateVersion7());
}
