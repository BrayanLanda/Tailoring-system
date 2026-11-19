using TailorService.Domain.Entities;

namespace TailorService.Domain.Repositories;

public interface ITailorRepository
{
    Task AddAsync(Tailor tailor, CancellationToken cancellationToken = default);
}
