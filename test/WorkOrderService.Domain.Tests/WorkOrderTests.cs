using WorkOrderService.Domain.Entities;
using WorkOrderService.Domain.Enums;
using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Tests;

public class WorkOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly PhoneNumber Phone = PhoneNumber.Of("300 123 4567");

    private static WorkOrder NewWorkOrder() =>
        WorkOrder.Create(SerialNumber.From(1), "Maria Lopez", Phone, "Black pants - hem", Now);

    private static WorkOrder AssignedWorkOrder()
    {
        var workOrder = NewWorkOrder();
        workOrder.AssignTo(new TailorId(Guid.NewGuid()));
        return workOrder;
    }

    // ---------- Create ----------

    [Fact]
    public void Create_ValidData_StartsAsReceivedAndRegular()
    {
        var workOrder = NewWorkOrder();

        Assert.Equal(WorkStatus.Received, workOrder.Status);
        Assert.Equal(WorkOrderType.Regular, workOrder.Type);
        Assert.Equal(SerialNumber.From(1), workOrder.SerialNumber);
        Assert.Equal(Phone, workOrder.CustomerPhone);
        Assert.Equal(Now, workOrder.CreatedAt);
        Assert.Null(workOrder.TailorId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyCustomerName_ThrowsDomainException(string customerName)
    {
        Assert.Throws<DomainException>(() =>
            WorkOrder.Create(SerialNumber.From(1), customerName, Phone, "Pants", Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyGarmentDescription_ThrowsDomainException(string garment)
    {
        Assert.Throws<DomainException>(() =>
            WorkOrder.Create(SerialNumber.From(1), "Maria Lopez", Phone, garment, Now));
    }

    // ---------- AssignTo ----------

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
        var workOrder = AssignedWorkOrder();

        Assert.Throws<DomainException>(() =>
            workOrder.AssignTo(new TailorId(Guid.NewGuid())));
    }

    // ---------- StartWork ----------

    [Fact]
    public void StartWork_WhenAssigned_BecomesInProgress()
    {
        var workOrder = AssignedWorkOrder();

        workOrder.StartWork();

        Assert.Equal(WorkStatus.InProgress, workOrder.Status);
    }

    [Fact]
    public void StartWork_WhenReceived_ThrowsDomainException()
    {
        var workOrder = NewWorkOrder();

        Assert.Throws<DomainException>(() => workOrder.StartWork());
    }

    // ---------- MarkDeliveredByTailor ----------

    [Fact]
    public void MarkDeliveredByTailor_FromAssigned_RecordsTheTimestamp()
    {
        var workOrder = AssignedWorkOrder();
        var deliveredAt = Now.AddDays(2);

        workOrder.MarkDeliveredByTailor(deliveredAt);

        Assert.Equal(WorkStatus.DeliveredByTailor, workOrder.Status);
        Assert.Equal(deliveredAt, workOrder.DeliveredByTailorAt);
    }

    [Fact]
    public void MarkDeliveredByTailor_FromInProgress_IsAllowed()
    {
        var workOrder = AssignedWorkOrder();
        workOrder.StartWork();

        workOrder.MarkDeliveredByTailor(Now.AddDays(2));

        Assert.Equal(WorkStatus.DeliveredByTailor, workOrder.Status);
    }

    [Fact]
    public void MarkDeliveredByTailor_WhenReceived_ThrowsDomainException()
    {
        var workOrder = NewWorkOrder();

        Assert.Throws<DomainException>(() =>
            workOrder.MarkDeliveredByTailor(Now.AddDays(1)));
    }

    [Fact]
    public void MarkDeliveredByTailor_BeforeCreationDate_ThrowsDomainException()
    {
        var workOrder = AssignedWorkOrder();

        Assert.Throws<DomainException>(() =>
            workOrder.MarkDeliveredByTailor(Now.AddHours(-1)));
    }

    // ---------- DeliverToCustomer ----------

    [Fact]
    public void DeliverToCustomer_WhenDeliveredByTailor_RecordsTheTimestamp()
    {
        var workOrder = AssignedWorkOrder();
        workOrder.MarkDeliveredByTailor(Now.AddDays(2));
        var deliveredAt = Now.AddDays(3);

        workOrder.DeliverToCustomer(deliveredAt);

        Assert.Equal(WorkStatus.DeliveredToCustomer, workOrder.Status);
        Assert.Equal(deliveredAt, workOrder.DeliveredToCustomerAt);
    }

    [Fact]
    public void DeliverToCustomer_WhenTailorHasNotDelivered_ThrowsDomainException()
    {
        var workOrder = AssignedWorkOrder();

        Assert.Throws<DomainException>(() =>
            workOrder.DeliverToCustomer(Now.AddDays(3)));
    }
}
