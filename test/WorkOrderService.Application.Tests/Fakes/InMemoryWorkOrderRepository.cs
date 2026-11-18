using WorkOrderService.Domain.Entities;
using WorkOrderService.Domain.Repositories;

namespace WorkOrderService.Application.Tests.Fakes;

internal sealed class InMemoryWorkOrderRepository : IWorkOrderRepository
{
    public List<WorkOrder> WorkOrders { get; } = [];

    public Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        WorkOrders.Add(workOrder);
        return Task.CompletedTask;
    }
}
