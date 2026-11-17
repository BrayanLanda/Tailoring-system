using WorkOrderService.Domain.Exceptions;

namespace WorkOrderService.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    private Money(decimal amount) => Amount = amount;
    public static Money Zero => new(0m);
    public static Money Of(decimal amount)
    {
        if(amount<0)
            throw new DomainException("Money cannot be negative");
        return new Money(amount);
    }
    public static Money operator +(Money left, Money right) => 
        new(left.Amount + right.Amount);
    public static Money operator -(Money left, Money right) =>
        Of(left.Amount - right.Amount);
}
