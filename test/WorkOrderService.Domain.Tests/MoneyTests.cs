using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Of_NegativeAmount_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Money.Of(-1m));
    }

    [Fact]
    public void Of_ValidAmount_KeepsTheAmount()
    {
        var money = Money.Of(25000m);
        Assert.Equal(25000m, money.Amount);
    }

    [Fact]
    public void TwoMoneyWithSameAmount_AreEquals()
    {
        Assert.Equal(Money.Of(100m), Money.Of(100m));
    }

    [Fact]
    public void Add_SumsBothAmounts()
    {
        var total = Money.Of(100m) + Money.Of(50m);
        Assert.Equal(Money.Of(150m), total);
    }

    [Fact]
    public void Subtract_ReturnsTheDifference()
    {
        Assert.Equal(Money.Of(60m), Money.Of(100m) - Money.Of(40m));
    }

    [Fact]
    public void Subtract_WhenResultIsNegative_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Money.Of(10m) - Money.Of(20m));
    }
}
