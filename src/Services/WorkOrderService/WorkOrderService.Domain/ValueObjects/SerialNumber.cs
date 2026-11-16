using WorkOrderService.Domain.Exceptions;

namespace WorkOrderService.Domain.ValueObjects;

public sealed record SerialNumber
{
    public long Value { get; }
    private SerialNumber(long value) => Value = value;
    public static SerialNumber From(long value)
    {
        if(value <= 0)
            throw new DomainException("Serial number must be greater than zero");
        return new SerialNumber(value);
    }
    public override string ToString() => Value.ToString();
}
