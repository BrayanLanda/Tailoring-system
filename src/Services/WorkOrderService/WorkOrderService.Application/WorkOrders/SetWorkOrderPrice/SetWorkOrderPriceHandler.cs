using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Exceptions;
using WorkOrderService.Domain.Repositories;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.WorkOrders.SetWorkOrderPrice;

public sealed class SetWorkOrderPriceHandler(
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<SetWorkOrderPriceResult> HandleAsync(
        SetWorkOrderPriceCommand command,
        CancellationToken cancellationToken = default)
    {
        var price = Money.Of(command.Amount);
        var workOrder = await workOrderRepository.GetByIdAsync(command.WorkOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(command.WorkOrderId);

        workOrder.SetPrice(price);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SetWorkOrderPriceResult(workOrder.Id, price);
    }
}
