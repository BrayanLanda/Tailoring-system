using IdentityService.Application.Abstractions;
using IdentityService.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(IdentityDbContext dbContext) : IUnitOfWork
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
