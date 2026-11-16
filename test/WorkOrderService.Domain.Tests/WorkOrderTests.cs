using WorkOrderService.Domain.Entities;
using WorkOrderService.Domain.Enums;
using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Tests;

public class WorkOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private static WorkOrder NewWorkOrder() =>
        WorkOrder.Create(SerialNumber.From(1), "Maria Lopez", "Black pants - hem", Now);

    [Fact]
    public void Create_ValidData_StartsAsReceivedAndRegular()
    {
        var workOrder = NewWorkOrder();

        Assert.Equal(WorkStatus.Received, workOrder.Status);
        Assert.Equal(WorkOrderType.Regular, workOrder.Type);
        Assert.Equal(SerialNumber.From(1), workOrder.SerialNumber);
        Assert.Equal(Now, workOrder.CreatedAt);
        Assert.Null(workOrder.TailorId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_EmptyCustomerName_ThrowsDomainException(string
     customerName)
    {
        Assert.Throws<DomainException>(() =>
            WorkOrder.Create(SerialNumber.From(1), customerName, "Pants", Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_EmptyGarmentDescription_ThrowsDomainException(string garment)
    {
        Assert.Throws<DomainException>(() => 
            WorkOrder.Create(SerialNumber.From(1), "Maria Lopez", garment, Now));
    }

    [Fact]
    public void AssignTo_WhenReceived_BecomesAssigned()
    {
        var workOrder = NewWorkOrder();
        var tailorId = new TailorId(Guid.NewGuid());

        workOrder.AssignTo(tailorId);
        
        Assert.Equal(WorkStatus.Assigned, workOrder.Status);
        Assert.Equal(tailorId, workOrder.TailorId);
    }

    [Fact]
    public void AssignTo_WhenAlreadyAssigned_ThrowsDomainException()
    {
        var workOrder = NewWorkOrder();
        workOrder.AssignTo(new TailorId(Guid.NewGuid()));

        Assert.Throws<DomainException>(() => 
            workOrder.AssignTo(new TailorId(Guid.NewGuid())));
    }
}
