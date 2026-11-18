using WorkOrderService.Application.Abstractions;

namespace WorkOrderService.Infrastructure.Persistence;

public sealed class EfUnitOfWork(WorkOrderDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
