using Microsoft.EntityFrameworkCore;
using TailorService.Domain.Entities;
using TailorService.Domain.Repositories;
using TailorService.Domain.ValueObjects;

namespace TailorService.Infrastructure.Persistence.Repositories;

internal sealed class EfTailorRepository(TailorDbContext dbContext) : ITailorRepository
{
    public async Task AddAsync(Tailor tailor, CancellationToken cancellationToken = default) =>
        await dbContext.Tailors.AddAsync(tailor, cancellationToken);

    public Task<Tailor?> GetByIdAsync(TailorId id, CancellationToken cancellationToken = default) =>
        dbContext.Tailors.SingleOrDefaultAsync(tailor => tailor.Id == id, cancellationToken);
}
