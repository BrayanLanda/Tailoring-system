using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Exceptions;
using WorkOrderService.Application.Tailors;
using WorkOrderService.Application.Tests.Fakes;
using WorkOrderService.Application.WorkOrders.AssignTailor;
using WorkOrderService.Domain.Entities;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.Tests.WorkOrders.AssignTailor;

public sealed class AssignTailorHandlerTests
{
    [Fact]
    public async Task HandleAsync_ActiveTailor_AssignsAndSavesWorkOrder()
    {
        var workOrder = CreateWorkOrder();
        var tailorId = Guid.NewGuid();
        var workOrders = new InMemoryWorkOrderRepository();
        workOrders.WorkOrders.Add(workOrder);
        var tailors = new InMemoryAssignableTailorRepository();
        tailors.Tailors.Add(new AssignableTailor(tailorId, true, DateTimeOffset.UtcNow));
        var unitOfWork = new CountingUnitOfWork();
        var handler = new AssignTailorHandler(workOrders, tailors, unitOfWork);

        var result = await handler.HandleAsync(new AssignTailorCommand(workOrder.Id, tailorId));

        Assert.Equal(workOrder.Id, result.WorkOrderId);
        Assert.Equal(tailorId, result.TailorId);
        Assert.Equal(new TailorId(tailorId), workOrder.TailorId);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_InactiveTailor_RejectsAssignmentWithoutSaving()
    {
        var workOrder = CreateWorkOrder();
        var tailorId = Guid.NewGuid();
        var workOrders = new InMemoryWorkOrderRepository();
        workOrders.WorkOrders.Add(workOrder);
        var tailors = new InMemoryAssignableTailorRepository();
        tailors.Tailors.Add(new AssignableTailor(tailorId, false, DateTimeOffset.UtcNow));
        var unitOfWork = new CountingUnitOfWork();
        var handler = new AssignTailorHandler(workOrders, tailors, unitOfWork);

        await Assert.ThrowsAsync<TailorNotAssignableException>(() =>
            handler.HandleAsync(new AssignTailorCommand(workOrder.Id, tailorId)));

        Assert.Null(workOrder.TailorId);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_UnknownTailor_RejectsAssignmentWithoutSaving()
    {
        var workOrder = CreateWorkOrder();
        var workOrders = new InMemoryWorkOrderRepository();
        workOrders.WorkOrders.Add(workOrder);
        var unitOfWork = new CountingUnitOfWork();
        var handler = new AssignTailorHandler(workOrders, new InMemoryAssignableTailorRepository(), unitOfWork);

        await Assert.ThrowsAsync<TailorNotAssignableException>(() =>
            handler.HandleAsync(new AssignTailorCommand(workOrder.Id, Guid.NewGuid())));

        Assert.Null(workOrder.TailorId);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static WorkOrder CreateWorkOrder() => WorkOrder.Create(
        SerialNumber.From(1),
        "Maria Lopez",
        PhoneNumber.Of("3001234567"),
        "Black pants",
        [AlterationJob.Of("Hem")],
        DateTimeOffset.Parse("2026-11-18T15:00:00Z"));

    private sealed class InMemoryAssignableTailorRepository : IAssignableTailorRepository
    {
        public List<AssignableTailor> Tailors { get; } = [];

        public Task<AssignableTailor?> GetByIdAsync(Guid tailorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tailors.SingleOrDefault(tailor => tailor.TailorId == tailorId));
    }
}
