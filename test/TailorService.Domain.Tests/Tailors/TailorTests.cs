using TailorService.Domain.Entities;
using TailorService.Domain.Events;
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

    [Fact]
    public void Create_RaisesTailorRegistered()
    {
        var tailor = Tailor.Create("Maria Lopez");

        var registered = Assert.IsType<TailorRegistered>(Assert.Single(tailor.DomainEvents));
        Assert.Equal(tailor.Id, registered.TailorId);
        Assert.Equal("Maria Lopez", registered.Name);
    }

    [Fact]
    public void Deactivate_WhenActive_BecomesInactiveAndRaisesTailorDeactivated()
    {
        var tailor = Tailor.Create("Maria Lopez");

        tailor.Deactivate();

        Assert.False(tailor.IsActive);
        var deactivated = tailor.DomainEvents.OfType<TailorDeactivated>().Single();
        Assert.Equal(tailor.Id, deactivated.TailorId);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_DoesNothing()
    {
        var tailor = Tailor.Create("Maria Lopez");
        tailor.Deactivate();

        tailor.Deactivate();

        Assert.False(tailor.IsActive);
        Assert.Single(tailor.DomainEvents.OfType<TailorDeactivated>());
    }

    [Fact]
    public void Reactivate_WhenInactive_BecomesActiveAndRaisesTailorReactivated()
    {
        var tailor = Tailor.Create("Maria Lopez");
        tailor.Deactivate();

        tailor.Reactivate();

        Assert.True(tailor.IsActive);
        var reactivated = tailor.DomainEvents.OfType<TailorReactivated>().Single();
        Assert.Equal(tailor.Id, reactivated.TailorId);
    }

    [Fact]
    public void Reactivate_WhenAlreadyActive_DoesNothing()
    {
        var tailor = Tailor.Create("Maria Lopez");

        tailor.Reactivate();

        Assert.True(tailor.IsActive);
        Assert.Empty(tailor.DomainEvents.OfType<TailorReactivated>());
    }
}
