using TailorService.Application.Abstractions;

namespace TailorService.Application.Tests.Fakes;

internal sealed class CountingUnitOfWork : IUnitOfWork
{
    public int SaveChangesCalls { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return Task.CompletedTask;
    }
}
