using TailorService.Domain.Entities;
using TailorService.Domain.Repositories;

namespace TailorService.Application.Tests.Fakes;

internal sealed class InMemoryTailorRepository : ITailorRepository
{
    public List<Tailor> Tailors { get; } = [];

    public Task AddAsync(Tailor tailor, CancellationToken cancellationToken = default)
    {
        Tailors.Add(tailor);
        return Task.CompletedTask;
    }
}
