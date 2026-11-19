using TailorService.Domain.Entities;
using TailorService.Domain.Exceptions;
using TailorService.Domain.ValueObjects;

namespace TailorService.Domain.Tests.Tailors;

public sealed class TailorTests
{
    [Fact]
    public void Create_ValidName_StartsActive()
    {
        var tailor = Tailor.Create("Maria Lopez");

        Assert.Equal("Maria Lopez", tailor.Name);
        Assert.True(tailor.IsActive);
        Assert.NotEqual(default(TailorId), tailor.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyName_ThrowsDomainException(string name)
    {
        Assert.Throws<DomainException>(() => Tailor.Create(name));
    }
}
