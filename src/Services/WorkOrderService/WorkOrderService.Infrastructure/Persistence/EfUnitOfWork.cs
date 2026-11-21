using Microsoft.EntityFrameworkCore;
using Npgsql;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Exceptions;

namespace WorkOrderService.Infrastructure.Persistence;

public sealed class EfUnitOfWork(WorkOrderDbContext dbContext) : IUnitOfWork
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
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A unique index was violated: someone else got there first. The caller can retry.
            throw new ConcurrencyConflictException(exception);
        }
    }
}
