using WorkOrderService.Application.Tests.Fakes;
using WorkOrderService.Application.WorkOrders.RegisterPayment;
using WorkOrderService.Domain.Entities;
using WorkOrderService.Domain.Enums;
using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.Tests.WorkOrders.RegisterPayment;

public sealed class RegisterPaymentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_ValidPartialPayment_RegistersAndSaves()
    {
        var workOrder = CreatePricedWorkOrder();
        var repository = new InMemoryWorkOrderRepository();
        repository.WorkOrders.Add(workOrder);
        var unitOfWork = new CountingUnitOfWork();
        var paymentId = PaymentId.New();
        var handler = CreateHandler(repository, unitOfWork);

        var result = await handler.HandleAsync(
            new RegisterPaymentCommand(workOrder.Id, paymentId, 10000m));

        var payment = Assert.Single(workOrder.Payments);
        Assert.Equal(paymentId, payment.Id);
        Assert.Equal(Money.Of(10000m), payment.Amount);
        Assert.Equal(workOrder.Id, result.WorkOrderId);
        Assert.Equal(paymentId, result.PaymentId);
        Assert.Equal(PaymentStatus.PartiallyPaid, result.PaymentStatus);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_AmountExceedsBalance_DoesNotSave()
    {
        var workOrder = CreatePricedWorkOrder();
        var repository = new InMemoryWorkOrderRepository();
        repository.WorkOrders.Add(workOrder);
        var unitOfWork = new CountingUnitOfWork();
        var handler = CreateHandler(repository, unitOfWork);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new RegisterPaymentCommand(workOrder.Id, PaymentId.New(), 30001m)));

        Assert.Empty(workOrder.Payments);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_RetriedPaymentId_DoesNotDuplicatePayment()
    {
        var workOrder = CreatePricedWorkOrder();
        var repository = new InMemoryWorkOrderRepository();
        repository.WorkOrders.Add(workOrder);
        var unitOfWork = new CountingUnitOfWork();
        var paymentId = PaymentId.New();
        var command = new RegisterPaymentCommand(workOrder.Id, paymentId, 10000m);
        var handler = CreateHandler(repository, unitOfWork);

        await handler.HandleAsync(command);
        await handler.HandleAsync(command);

        Assert.Single(workOrder.Payments);
        Assert.Equal(Money.Of(10000m), workOrder.AmountPaid);
    }

    private static RegisterPaymentHandler CreateHandler(
        InMemoryWorkOrderRepository repository,
        CountingUnitOfWork unitOfWork) =>
        new(repository, unitOfWork, new FixedTimeProvider(Now));

    private static WorkOrder CreatePricedWorkOrder()
    {
        var workOrder = WorkOrder.Create(
            SerialNumber.From(1),
            "Maria Lopez",
            PhoneNumber.Of("3001234567"),
            "Black pants",
            [AlterationJob.Of("Hem")],
            Now);
        workOrder.SetPrice(Money.Of(30000m));
        return workOrder;
    }
}
