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
        Assert.Equal(PaymentStatus.Unpriced, workOrder.PaymentStatus);
        Assert.Null(workOrder.Price);
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

    [Fact]
    public void DeliverToCustomer_WhenDeliveredByTailorAndPaid_RecordsTheTimestamp()
    {
        var workOrder = AssignedWorkOrder();
        workOrder.SetPrice(Money.Of(30000m));
        workOrder.RegisterPayment(PaymentId.New(), Money.Of(30000m), Now.AddDays(1));
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

    [Fact]
    public void SetPrice_ValidAmount_BecomesPriced()
    {
        var workOrder = NewWorkOrder();

        workOrder.SetPrice(Money.Of(25000m));

        Assert.Equal(Money.Of(25000m), workOrder.Price);
        Assert.Equal(PaymentStatus.Priced, workOrder.PaymentStatus);
    }

    [Fact]
    public void SetPrice_Zero_ThrowsDomainException()
    {
        var workOrder = NewWorkOrder();

        Assert.Throws<DomainException>(() => workOrder.SetPrice(Money.Zero));
    }

    [Fact]
    public void SetPrice_CanBeCorrectedWhileStillPriced()
    {
        var workOrder = NewWorkOrder();
        workOrder.SetPrice(Money.Of(20000m));

        workOrder.SetPrice(Money.Of(30000m));

        Assert.Equal(Money.Of(30000m), workOrder.Price);
    }

    private static WorkOrder PricedWorkOrder(decimal price = 30000m)
    {
        var workOrder = NewWorkOrder();
        workOrder.SetPrice(Money.Of(price));
        return workOrder;
    }

    [Fact]
    public void RegisterPayment_WithoutPrice_ThrowsDomainException()
    {
        var workOrder = NewWorkOrder();

        Assert.Throws<DomainException>(() =>
            workOrder.RegisterPayment(PaymentId.New(), Money.Of(10000m), Now));
    }

    [Fact]
    public void RegisterPayment_PartialAmount_BecomesPartiallyPaid()
    {
        var workOrder = PricedWorkOrder(30000m);

        workOrder.RegisterPayment(PaymentId.New(), Money.Of(10000m), Now);

        Assert.Equal(PaymentStatus.PartiallyPaid, workOrder.PaymentStatus);
        Assert.Equal(Money.Of(10000m), workOrder.AmountPaid);
        Assert.Equal(Money.Of(20000m), workOrder.Balance);
    }

    [Fact]
    public void RegisterPayment_FullAmount_BecomesPaid()
    {
        var workOrder = PricedWorkOrder(30000m);

        workOrder.RegisterPayment(PaymentId.New(), Money.Of(30000m), Now);

        Assert.Equal(PaymentStatus.Paid, workOrder.PaymentStatus);
        Assert.Equal(Money.Zero, workOrder.Balance);
    }

    [Fact]
    public void RegisterPayment_TwoPartialPaymentsThatCompleteThePrice_BecomesPaid()
    {
        var workOrder = PricedWorkOrder(30000m);

        workOrder.RegisterPayment(PaymentId.New(), Money.Of(10000m), Now);
        workOrder.RegisterPayment(PaymentId.New(), Money.Of(20000m), Now.AddDays(2));

        Assert.Equal(PaymentStatus.Paid, workOrder.PaymentStatus);
        Assert.Equal(2, workOrder.Payments.Count);
    }

    [Fact]
    public void RegisterPayment_ExceedingTheBalance_ThrowsDomainException()
    {
        var workOrder = PricedWorkOrder(30000m);
        workOrder.RegisterPayment(PaymentId.New(), Money.Of(20000m), Now);

        Assert.Throws<DomainException>(() =>
            workOrder.RegisterPayment(PaymentId.New(), Money.Of(15000m), Now));
    }

    [Fact]
    public void RegisterPayment_ZeroAmount_ThrowsDomainException()
    {
        var workOrder = PricedWorkOrder();

        Assert.Throws<DomainException>(() =>
            workOrder.RegisterPayment(PaymentId.New(), Money.Zero, Now));
    }

    [Fact]
    public void RegisterPayment_SamePaymentIdTwice_IsRegisteredOnlyOnce()
    {
        var workOrder = PricedWorkOrder(30000m);
        var paymentId = PaymentId.New();

        workOrder.RegisterPayment(paymentId, Money.Of(10000m), Now);
        workOrder.RegisterPayment(paymentId, Money.Of(10000m), Now);

        Assert.Single(workOrder.Payments);
        Assert.Equal(Money.Of(10000m), workOrder.AmountPaid);
    }

    [Fact]
    public void RegisterPayment_BeforeCreationDate_ThrowsDomainException()
    {
        var workOrder = PricedWorkOrder();

        Assert.Throws<DomainException>(() =>
            workOrder.RegisterPayment(PaymentId.New(), Money.Of(10000m), Now.AddHours(-1)));
    }

    [Fact]
    public void SetPrice_AfterPaymentsRegistered_ThrowsDomainException()
    {
        var workOrder = PricedWorkOrder(30000m);
        workOrder.RegisterPayment(PaymentId.New(), Money.Of(10000m), Now);

        Assert.Throws<DomainException>(() => workOrder.SetPrice(Money.Of(40000m)));
    }

    [Fact]
    public void DeliverToCustomer_WhenNotFullyPaid_ThrowsDomainException()
    {
        var workOrder = AssignedWorkOrder();
        workOrder.SetPrice(Money.Of(30000m));
        workOrder.RegisterPayment(PaymentId.New(), Money.Of(10000m), Now);
        workOrder.MarkDeliveredByTailor(Now.AddDays(2));

        Assert.Throws<DomainException>(() =>
            workOrder.DeliverToCustomer(Now.AddDays(3)));
    }
}
