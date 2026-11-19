using TailorService.Application.Tests.Fakes;
using TailorService.Application.Tailors.RegisterTailor;
using TailorService.Domain.Exceptions;

namespace TailorService.Application.Tests.Tailors.RegisterTailor;

public sealed class RegisterTailorHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidName_RegistersActiveTailorAndSaves()
    {
        var repository = new InMemoryTailorRepository();
        var unitOfWork = new CountingUnitOfWork();
        var handler = new RegisterTailorHandler(repository, unitOfWork);

        var result = await handler.HandleAsync(new RegisterTailorCommand("Maria Lopez"));

        var tailor = Assert.Single(repository.Tailors);
        Assert.Equal(tailor.Id, result.TailorId);
        Assert.Equal("Maria Lopez", result.Name);
        Assert.True(result.IsActive);
        Assert.True(tailor.IsActive);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_EmptyName_DoesNotPersist(string name)
    {
        var repository = new InMemoryTailorRepository();
        var unitOfWork = new CountingUnitOfWork();
        var handler = new RegisterTailorHandler(repository, unitOfWork);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new RegisterTailorCommand(name)));

        Assert.Empty(repository.Tailors);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }
}
