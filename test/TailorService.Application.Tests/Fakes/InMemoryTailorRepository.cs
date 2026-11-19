using TailorService.Domain.Entities;
using TailorService.Domain.Repositories;
using TailorService.Domain.ValueObjects;

namespace TailorService.Application.Tests.Fakes;

internal sealed class InMemoryTailorRepository : ITailorRepository
{
    public List<Tailor> Tailors { get; } = [];

    public Task AddAsync(Tailor tailor, CancellationToken cancellationToken = default)
    {
        Tailors.Add(tailor);
        return Task.CompletedTask;
    }

    public Task<Tailor?> GetByIdAsync(TailorId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tailors.SingleOrDefault(tailor => tailor.Id == id));
}
