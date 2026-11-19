using Microsoft.EntityFrameworkCore;
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
    }
}
