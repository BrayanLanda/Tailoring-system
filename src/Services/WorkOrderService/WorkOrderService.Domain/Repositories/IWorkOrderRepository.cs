using WorkOrderService.Domain.Entities;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Repositories;

public interface IWorkOrderRepository
{
    Task<WorkOrder?> GetByIdAsync(WorkOrderId id, CancellationToken cancellationToken = default);
    Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
}
