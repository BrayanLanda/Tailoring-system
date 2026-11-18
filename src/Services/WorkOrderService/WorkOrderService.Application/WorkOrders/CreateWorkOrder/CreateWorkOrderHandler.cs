using WorkOrderService.Application.Abstractions;
using WorkOrderService.Domain.Entities;
using WorkOrderService.Domain.Repositories;
using WorkOrderService.Domain.Services;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.WorkOrders.CreateWorkOrder;

public sealed class CreateWorkOrderHandler(
    IWorkOrderRepository workOrderRepository,
    ISerialNumberGenerator serialNumberGenerator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<CreateWorkOrderResult> HandleAsync(
        CreateWorkOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var customerPhone = PhoneNumber.Of(command.CustomerPhone);
        var jobs = command.Jobs
            .Select(job => AlterationJob.Of(job.Description, job.Quantity))
            .ToArray();

        var serialNumber = await serialNumberGenerator.GetNextAsync(cancellationToken);
        var workOrder = WorkOrder.Create(
            serialNumber,
            command.CustomerName,
            customerPhone,
            command.GarmentDescription,
            jobs,
            timeProvider.GetUtcNow());

        await workOrderRepository.AddAsync(workOrder, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateWorkOrderResult(workOrder.Id, workOrder.SerialNumber);
    }
}
