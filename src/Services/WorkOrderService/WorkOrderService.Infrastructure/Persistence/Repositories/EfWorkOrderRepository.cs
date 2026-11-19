using Microsoft.EntityFrameworkCore;
using WorkOrderService.Domain.Entities;
using WorkOrderService.Domain.Repositories;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Infrastructure.Persistence.Repositories;

public sealed class EfWorkOrderRepository(WorkOrderDbContext dbContext) : IWorkOrderRepository
{
    public Task<WorkOrder?> GetByIdAsync(
        WorkOrderId id,
        CancellationToken cancellationToken = default) =>
        dbContext.WorkOrders
            .Include(workOrder => workOrder.Payments)
            .Include(workOrder => workOrder.Jobs)
            .AsSplitQuery()
            .SingleOrDefaultAsync(workOrder => workOrder.Id == id, cancellationToken);

    public async Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default) =>
        await dbContext.WorkOrders.AddAsync(workOrder, cancellationToken);
}
