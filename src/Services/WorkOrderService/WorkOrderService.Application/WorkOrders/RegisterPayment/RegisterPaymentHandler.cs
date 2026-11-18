using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Exceptions;
using WorkOrderService.Domain.Repositories;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.WorkOrders.RegisterPayment;

public sealed class RegisterPaymentHandler(
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<RegisterPaymentResult> HandleAsync(
        RegisterPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var amount = Money.Of(command.Amount);
        var workOrder = await workOrderRepository.GetByIdAsync(command.WorkOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(command.WorkOrderId);

        workOrder.RegisterPayment(command.PaymentId, amount, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterPaymentResult(workOrder.Id, command.PaymentId, workOrder.PaymentStatus);
    }
}
