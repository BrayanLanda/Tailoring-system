using WorkOrderService.Application.WorkOrders.CreateWorkOrder;
using WorkOrderService.Application.Tests.Fakes;
using WorkOrderService.Domain.Enums;
using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.Tests.WorkOrders.CreateWorkOrder;

public sealed class CreateWorkOrderHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_ValidCommand_CreatesAndSavesWorkOrder()
    {
        var serialGenerator = new SequentialSerialNumberGenerator();
        var repository = new InMemoryWorkOrderRepository();
        var unitOfWork = new CountingUnitOfWork();
        var handler = CreateHandler(serialGenerator, repository, unitOfWork);
        var command = new CreateWorkOrderCommand(
            "Maria Lopez",
            "+57 300 123 4567",
            "Black pants",
            [new CreateWorkOrderJob("Hem", 2), new CreateWorkOrderJob("Seam", 1)]);

        var result = await handler.HandleAsync(command);

        var workOrder = Assert.Single(repository.WorkOrders);
        Assert.Equal(workOrder.Id, result.Id);
        Assert.Equal(SerialNumber.From(1), result.SerialNumber);
        Assert.Equal(SerialNumber.From(1), workOrder.SerialNumber);
        Assert.Equal("3001234567", workOrder.CustomerPhone.Value);
        Assert.Equal(["Hem", "Seam"], workOrder.Jobs.Select(job => job.Description));
        Assert.Equal(Now, workOrder.CreatedAt);
        Assert.Equal(1, serialGenerator.Calls);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_InvalidPhone_DoesNotRequestSerialOrSave()
    {
        var serialGenerator = new SequentialSerialNumberGenerator();
        var repository = new InMemoryWorkOrderRepository();
        var unitOfWork = new CountingUnitOfWork();
        var handler = CreateHandler(serialGenerator, repository, unitOfWork);
        var command = new CreateWorkOrderCommand(
            "Maria Lopez",
            "123",
            "Black pants",
            [new CreateWorkOrderJob("Hem", 1)]);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));

        Assert.Empty(repository.WorkOrders);
        Assert.Equal(0, serialGenerator.Calls);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_InvalidJob_DoesNotRequestSerialOrSave()
    {
        var serialGenerator = new SequentialSerialNumberGenerator();
        var repository = new InMemoryWorkOrderRepository();
        var unitOfWork = new CountingUnitOfWork();
        var handler = CreateHandler(serialGenerator, repository, unitOfWork);
        var command = new CreateWorkOrderCommand(
            "Maria Lopez",
            "3001234567",
            "Black pants",
            [new CreateWorkOrderJob("Hem", 0)]);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));

        Assert.Empty(repository.WorkOrders);
        Assert.Equal(0, serialGenerator.Calls);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static CreateWorkOrderHandler CreateHandler(
        SequentialSerialNumberGenerator serialGenerator,
        InMemoryWorkOrderRepository repository,
        CountingUnitOfWork unitOfWork) =>
        new(repository, serialGenerator, unitOfWork, new FixedTimeProvider(Now));
}
