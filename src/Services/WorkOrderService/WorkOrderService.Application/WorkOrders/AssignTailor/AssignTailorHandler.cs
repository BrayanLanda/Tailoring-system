using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Exceptions;
using WorkOrderService.Domain.Repositories;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.WorkOrders.AssignTailor;

public sealed class AssignTailorHandler(
    IWorkOrderRepository workOrderRepository,
    IAssignableTailorRepository assignableTailorRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<AssignTailorResult> HandleAsync(
        AssignTailorCommand command,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await workOrderRepository.GetByIdAsync(command.WorkOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(command.WorkOrderId);

        var tailor = await assignableTailorRepository.GetByIdAsync(command.TailorId, cancellationToken);
        if (tailor is null || !tailor.IsActive)
            throw new TailorNotAssignableException(command.TailorId);

        workOrder.AssignTo(new TailorId(command.TailorId), command.ReassignmentReason);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssignTailorResult(workOrder.Id, command.TailorId);
    }
}
