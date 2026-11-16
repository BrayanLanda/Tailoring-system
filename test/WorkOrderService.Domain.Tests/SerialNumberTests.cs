using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Tests;

public class SerialNumberTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void From_ZeroOrNegative_ThrowsDomainException(long value)
    {
        Assert.Throws<DomainException>(() => SerialNumber.From(value));
    }

    [Fact]
    public void From_PositiveValue_KeepsTheValue()
    {
        var serial = SerialNumber.From(1042);
        Assert.Equal(1042, serial.Value);
    }

    [Fact]
    public void TwoSerialsWithSameValue_AreEqual()
    {
        Assert.Equal(SerialNumber.From(7), SerialNumber.From(7));
    }
}
