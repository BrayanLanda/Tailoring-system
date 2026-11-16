using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("2123456789")]
    [InlineData("30012345678")]
    [InlineData("300-ABC-4567")]
    public void Of_InvalidValue_ThrowsDomainException(string value)
    {
        Assert.Throws<DomainException>(() => PhoneNumber.Of(value));
    }

    [Theory]
    [InlineData("3001234567")]
    [InlineData("300 123 4567")]
    [InlineData("(300) 123-4567")]
    [InlineData("+57 300 123 4567")]
    public void Of_ValidFormats_AreNormalizedToTenDigits(string value)
    {
        Assert.Equal("3001234567", PhoneNumber.Of(value).Value);
    }

    [Fact]
    public void TwoPhonesWithSameDigits_AreEqual()
    {
        Assert.Equal(PhoneNumber.Of("300 123 4567"), PhoneNumber.Of("3001234567"));
    }
}
