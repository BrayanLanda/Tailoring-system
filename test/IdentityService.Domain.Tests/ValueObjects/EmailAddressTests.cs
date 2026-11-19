using IdentityService.Domain.Exceptions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Domain.Tests.ValueObjects;

public class EmailAddressTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("Ana <ana@example.com>")]
    public void Of_InvalidValue_ThrowsDomainException(string value)
    {
        Assert.Throws<DomainException>(() => EmailAddress.Of(value));
    }

    [Fact]
    public void Of_ValidValue_IsTrimmedAndLowerCased()
    {
        var email = EmailAddress.Of("  Ana.Gomez@Example.com ");

        Assert.Equal("ana.gomez@example.com", email.Value);
    }

    [Fact]
    public void TwoEmailsWithTheSameAddress_AreEqual()
    {
        Assert.Equal(EmailAddress.Of("ANA@example.com"), EmailAddress.Of("ana@example.com"));
    }
}
