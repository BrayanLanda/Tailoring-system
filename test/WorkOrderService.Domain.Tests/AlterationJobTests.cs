using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Tests;

public class AlterationJobTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Of_EmptyDescription_ThrowsDomainException(string description)
    {
        Assert.Throws<DomainException>(() => AlterationJob.Of(description, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Of_ZeroOrNegativeQuantity_ThrowsDomainException(int quantity)
    {
        Assert.Throws<DomainException>(() => AlterationJob.Of("Hem", quantity));
    }

    [Fact]
    public void Of_ValidData_KeepsTheQuantityAndTrimsTheDescription()
    {
        var job = AlterationJob.Of("  Hem ", 3);

        Assert.Equal("Hem", job.Description);
        Assert.Equal(3, job.Quantity);
    }

    [Fact]
    public void Of_WithoutQuantity_DefaultsToOne()
    {
        Assert.Equal(1, AlterationJob.Of("Zipper").Quantity);
    }

    [Fact]
    public void TwoJobsWithTheSameData_AreEqual()
    {
        Assert.Equal(AlterationJob.Of("Hem", 2), AlterationJob.Of("Hem", 2));
    }

}
