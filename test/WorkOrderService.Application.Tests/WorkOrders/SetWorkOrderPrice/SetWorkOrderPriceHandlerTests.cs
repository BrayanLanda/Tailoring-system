using WorkOrderService.Application.Tests.Fakes;
using WorkOrderService.Application.Exceptions;
using WorkOrderService.Application.WorkOrders.SetWorkOrderPrice;
using WorkOrderService.Domain.Entities;
using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.Tests.WorkOrders.SetWorkOrderPrice;

public sealed class SetWorkOrderPriceHandlerTests
{
    [Fact]
    public async Task HandleAsync_ExistingWorkOrder_SetsPriceAndSaves()
    {
        var workOrder = CreateWorkOrder();
        var repository = new InMemoryWorkOrderRepository();
        repository.WorkOrders.Add(workOrder);
        var unitOfWork = new CountingUnitOfWork();
        var handler = new SetWorkOrderPriceHandler(repository, unitOfWork);

        var result = await handler.HandleAsync(new SetWorkOrderPriceCommand(workOrder.Id, 25000m));

        Assert.Equal(workOrder.Id, result.WorkOrderId);
        Assert.Equal(Money.Of(25000m), result.Price);
        Assert.Equal(Money.Of(25000m), workOrder.Price);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_ZeroPrice_DoesNotSave()
    {
        var workOrder = CreateWorkOrder();
        var repository = new InMemoryWorkOrderRepository();
        repository.WorkOrders.Add(workOrder);
        var unitOfWork = new CountingUnitOfWork();
        var handler = new SetWorkOrderPriceHandler(repository, unitOfWork);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new SetWorkOrderPriceCommand(workOrder.Id, 0m)));

        Assert.Null(workOrder.Price);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WorkOrderDoesNotExist_ThrowsAndDoesNotSave()
    {
        var repository = new InMemoryWorkOrderRepository();
        var unitOfWork = new CountingUnitOfWork();
        var handler = new SetWorkOrderPriceHandler(repository, unitOfWork);

        await Assert.ThrowsAsync<WorkOrderNotFoundException>(() =>
            handler.HandleAsync(new SetWorkOrderPriceCommand(WorkOrderId.New(), 25000m)));

        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static WorkOrder CreateWorkOrder() => WorkOrder.Create(
        SerialNumber.From(1),
        "Maria Lopez",
        PhoneNumber.Of("3001234567"),
        "Black pants",
        [AlterationJob.Of("Hem")],
        DateTimeOffset.Parse("2026-11-18T15:00:00Z"));
}
