using Microsoft.Extensions.DependencyInjection;
using TailorService.Application.Abstractions;
using TailorService.Application.Exceptions;
using TailorService.Application.Tailors.RegisterTailor;
using TailorService.Domain.Entities;
using TailorService.Domain.Repositories;
using TailorService.Domain.ValueObjects;

namespace TailorService.Infrastructure.IntegrationTests;

[Collection(TailorPostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TailorPersistenceTests(TailorPostgresFixture postgres)
{
    [Fact]
    public async Task RegisterTailor_InOneRequest_ReloadsActiveTailorInAnotherRequest()
    {
        await using var writeRequest = postgres.NewRequest();
        var handler = new RegisterTailorHandler(
            writeRequest.ServiceProvider.GetRequiredService<ITailorRepository>(),
            writeRequest.ServiceProvider.GetRequiredService<IUnitOfWork>());

        var result = await handler.HandleAsync(new RegisterTailorCommand("Maria Lopez"));

        await using var readRequest = postgres.NewRequest();
        var reloaded = await readRequest.ServiceProvider
            .GetRequiredService<ITailorRepository>()
            .GetByIdAsync(result.TailorId);

        Assert.NotNull(reloaded);
        Assert.Equal(result.TailorId, reloaded.Id);
        Assert.Equal("Maria Lopez", reloaded.Name);
        Assert.True(reloaded.IsActive);
        Assert.Empty(reloaded.DomainEvents);
    }

    [Fact]
    public async Task TwoConcurrentDeactivations_SecondSaveConflicts()
    {
        var tailorId = await RegisterTailorAsync();

        await using var firstRequest = postgres.NewRequest();
        await using var secondRequest = postgres.NewRequest();
        var tailorA = await Repository(firstRequest).GetByIdAsync(tailorId);
        var tailorB = await Repository(secondRequest).GetByIdAsync(tailorId);
        Assert.NotNull(tailorA);
        Assert.NotNull(tailorB);

        tailorA.Deactivate();
        tailorB.Deactivate();

        await UnitOfWork(firstRequest).SaveChangesAsync();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => UnitOfWork(secondRequest).SaveChangesAsync());

        await using var verifyRequest = postgres.NewRequest();
        var reloaded = await Repository(verifyRequest).GetByIdAsync(tailorId);
        Assert.NotNull(reloaded);
        Assert.False(reloaded.IsActive);
    }

    private async Task<TailorId> RegisterTailorAsync()
    {
        await using var request = postgres.NewRequest();
        var handler = new RegisterTailorHandler(Repository(request), UnitOfWork(request));
        var result = await handler.HandleAsync(new RegisterTailorCommand("Ana Gomez"));
        return result.TailorId;
    }

    private static ITailorRepository Repository(AsyncServiceScope request) =>
        request.ServiceProvider.GetRequiredService<ITailorRepository>();

    private static IUnitOfWork UnitOfWork(AsyncServiceScope request) =>
        request.ServiceProvider.GetRequiredService<IUnitOfWork>();
}
