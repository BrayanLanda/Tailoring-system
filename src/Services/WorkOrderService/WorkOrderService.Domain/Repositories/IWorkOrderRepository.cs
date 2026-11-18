using WorkOrderService.Domain.Entities;

namespace WorkOrderService.Domain.Repositories;

public interface IWorkOrderRepository
{
    Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
}
