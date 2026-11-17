using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Tests;

public class WarrantyCompensationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Of_ZeroAmount_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            WarrantyCompensation.Of(Money.Zero, "Owner", Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Of_WithoutAuthorizer_ThrowsDomainException(string authorizedBy)
    {
        Assert.Throws<DomainException>(() =>
            WarrantyCompensation.Of(Money.Of(8000m), authorizedBy, Now));
    }

    [Fact]
    public void Of_ValidData_KeepsTheValuesAndTrimsTheAuthorizer()
    {
        var compensation = WarrantyCompensation.Of(Money.Of(8000m), "  Owner ", Now);

        Assert.Equal(Money.Of(8000m), compensation.Amount);
        Assert.Equal("Owner", compensation.AuthorizedBy);
        Assert.Equal(Now, compensation.AuthorizedAt);
    }
}
