using TailorService.Application.Exceptions;
using TailorService.Application.Tests.Fakes;
using TailorService.Application.Tailors.GetTailorById;
using TailorService.Domain.Entities;
using TailorService.Domain.ValueObjects;

namespace TailorService.Application.Tests.Tailors.GetTailorById;

public sealed class GetTailorByIdHandlerTests
{
    [Fact]
    public async Task HandleAsync_ExistingTailor_ReturnsItsPublicDetails()
    {
        var tailor = Tailor.Create("Maria Lopez");
        var repository = new InMemoryTailorRepository();
        await repository.AddAsync(tailor);
        var handler = new GetTailorByIdHandler(repository);

        var result = await handler.HandleAsync(new GetTailorByIdQuery(tailor.Id));

        Assert.Equal(tailor.Id, result.TailorId);
        Assert.Equal("Maria Lopez", result.Name);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task HandleAsync_MissingTailor_ThrowsNotFound()
    {
        var tailorId = TailorId.New();
        var handler = new GetTailorByIdHandler(new InMemoryTailorRepository());

        await Assert.ThrowsAsync<TailorNotFoundException>(() =>
            handler.HandleAsync(new GetTailorByIdQuery(tailorId)));
    }
}
