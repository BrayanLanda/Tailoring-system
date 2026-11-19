using TailorService.Domain.Entities;
using TailorService.Domain.ValueObjects;

namespace TailorService.Domain.Repositories;

public interface ITailorRepository
{
    Task AddAsync(Tailor tailor, CancellationToken cancellationToken = default);
    Task<Tailor?> GetByIdAsync(TailorId id, CancellationToken cancellationToken = default);
}
