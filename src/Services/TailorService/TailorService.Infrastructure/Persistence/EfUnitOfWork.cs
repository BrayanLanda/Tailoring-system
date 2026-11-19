using Microsoft.EntityFrameworkCore;
using TailorService.Application.Abstractions;
using TailorService.Application.Exceptions;

namespace TailorService.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(TailorDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
    }
}
