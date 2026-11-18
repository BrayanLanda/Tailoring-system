using WorkOrderService.Domain.Entities;
using WorkOrderService.Domain.Repositories;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.Tests.Fakes;

internal sealed class InMemoryWorkOrderRepository : IWorkOrderRepository
{
    public List<WorkOrder> WorkOrders { get; } = [];

    public Task<WorkOrder?> GetByIdAsync(WorkOrderId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(WorkOrders.SingleOrDefault(workOrder => workOrder.Id == id));

    public Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        WorkOrders.Add(workOrder);
        return Task.CompletedTask;
    }
}
