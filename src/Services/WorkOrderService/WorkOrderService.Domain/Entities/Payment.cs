using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Entities;

public class Payment
{
    public PaymentId Id { get; private set; }
    public Money Amount { get; private set; } = null!;
    public DateTimeOffset PaidAt { get; private set; }

    private Payment() { }

    internal static Payment Create(PaymentId id, Money amount, DateTimeOffset paidAt) =>
        new() { Id = id, Amount = amount, PaidAt = paidAt };
}
