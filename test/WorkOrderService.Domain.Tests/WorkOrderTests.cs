using WorkOrderService.Domain.Entities;
using WorkOrderService.Domain.Enums;
using WorkOrderService.Domain.Events;
using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Tests;

public class WorkOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly PhoneNumber Phone = PhoneNumber.Of("300 123 4567");
    private static readonly AlterationJob[] SampleJobs =
   [AlterationJob.Of("Hem", 3), AlterationJob.Of("Seam", 2)];

    private static WorkOrder NewWorkOrder() =>
    WorkOrder.Create(SerialNumber.From(1), "Maria Lopez", Phone, "Black pants", SampleJobs, Now);
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
        Assert.Equal(SampleJobs, workOrder.Jobs.ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyCustomerName_ThrowsDomainException(string customerName)
    {
        Assert.Throws<DomainException>(() =>
            WorkOrder.Create(SerialNumber.From(1), customerName, Phone, "Pants", SampleJobs, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyGarmentDescription_ThrowsDomainException(string garment)
    {
        Assert.Throws<DomainException>(() =>
            WorkOrder.Create(SerialNumber.From(1), "Maria Lopez", Phone, garment, SampleJobs, Now));
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

    private static WorkOrder NewWarranty(
     WorkOrderId? originalWorkOrderId = null,
     TailorId? originalTailorId = null) =>
     WorkOrder.CreateWarranty(
         SerialNumber.From(2), "Pedro Gomez", Phone,
         "Black pants - hem came undone", SampleJobs, Now,
         originalWorkOrderId, originalTailorId);

    [Fact]
    public void CreateWarranty_LinkedToOriginal_IsWarrantyWithNoCharge()
    {
        var originalId = WorkOrderId.New();
        var originalTailor = new TailorId(Guid.NewGuid());

        var warranty = NewWarranty(originalId, originalTailor);

        Assert.Equal(WorkOrderType.Warranty, warranty.Type);
        Assert.Equal(WorkStatus.Received, warranty.Status);
        Assert.Equal(PaymentStatus.NoCharge, warranty.PaymentStatus);
        Assert.Null(warranty.Price);
        Assert.Equal(originalId, warranty.OriginalWorkOrderId);
        Assert.Equal(originalTailor, warranty.OriginalTailorId);
        Assert.True(warranty.IsLinkedToOriginal);
    }

    [Fact]
    public void CreateWarranty_WithoutOriginal_IsNotLinked()
    {
        var warranty = NewWarranty();

        Assert.False(warranty.IsLinkedToOriginal);
        Assert.Null(warranty.OriginalTailorId);
    }

    [Fact]
    public void CreateWarranty_OriginalTailorWithoutOriginalWorkOrder_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            NewWarranty(originalWorkOrderId: null, originalTailorId: new TailorId(Guid.NewGuid())));
    }

    [Fact]
    public void SetPrice_OnWarranty_ThrowsDomainException()
    {
        var warranty = NewWarranty();

        var ex = Assert.Throws<DomainException>(() => warranty.SetPrice(Money.Of(10000m)));
        Assert.Contains("no charge", ex.Message);
    }

    [Fact]
    public void RegisterPayment_OnWarranty_ThrowsDomainException()
    {
        var warranty = NewWarranty();

        var ex = Assert.Throws<DomainException>(() =>
            warranty.RegisterPayment(PaymentId.New(), Money.Of(10000m), Now));
        Assert.Contains("no charge", ex.Message);
    }

    [Fact]
    public void AssignTo_WarrantyToTheOriginalTailor_DoesNotNeedAReason()
    {
        var originalTailor = new TailorId(Guid.NewGuid());
        var warranty = NewWarranty(WorkOrderId.New(), originalTailor);

        warranty.AssignTo(originalTailor);

        Assert.Equal(WorkStatus.Assigned, warranty.Status);
        Assert.Equal(originalTailor, warranty.TailorId);
        Assert.Null(warranty.ReassignmentReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AssignTo_WarrantyToAnotherTailorWithoutReason_ThrowsDomainException(string? reason)
    {
        var warranty = NewWarranty(WorkOrderId.New(), new TailorId(Guid.NewGuid()));

        Assert.Throws<DomainException>(() =>
            warranty.AssignTo(new TailorId(Guid.NewGuid()), reason));
    }

    [Fact]
    public void AssignTo_WarrantyToAnotherTailorWithReason_RecordsTheReason()
    {
        var warranty = NewWarranty(WorkOrderId.New(), new TailorId(Guid.NewGuid()));
        var otherTailor = new TailorId(Guid.NewGuid());

        warranty.AssignTo(otherTailor, "  Original tailor is on vacation ");

        Assert.Null(warranty.Compensation);
        Assert.Equal(otherTailor, warranty.TailorId);
        Assert.Equal("Original tailor is on vacation", warranty.ReassignmentReason);
    }

    [Fact]
    public void AssignTo_WarrantyWithoutOriginalTailor_AllowsAnyTailorWithoutReason()
    {
        var warranty = NewWarranty();

        warranty.AssignTo(new TailorId(Guid.NewGuid()));

        Assert.Equal(WorkStatus.Assigned, warranty.Status);
    }

    [Fact]
    public void DeliverToCustomer_Warranty_DoesNotRequirePayment()
    {
        var warranty = NewWarranty();
        warranty.AssignTo(new TailorId(Guid.NewGuid()));
        warranty.MarkDeliveredByTailor(Now.AddDays(1));

        warranty.DeliverToCustomer(Now.AddDays(2));

        Assert.Equal(WorkStatus.DeliveredToCustomer, warranty.Status);
    }

    private static WorkOrder WarrantyDoneByAnotherTailor()
    {
        var warranty = NewWarranty(WorkOrderId.New(), new TailorId(Guid.NewGuid()));
        warranty.AssignTo(new TailorId(Guid.NewGuid()), "Original tailor is on vacation");
        return warranty;
    }

    [Fact]
    public void AuthorizeCompensation_WarrantyDoneByAnotherTailor_RecordsIt()
    {
        var warranty = WarrantyDoneByAnotherTailor();
        var authorizedAt = Now.AddDays(3);

        warranty.AuthorizeCompensation(Money.Of(8000m), "Owner", authorizedAt);

        Assert.NotNull(warranty.Compensation);
        Assert.Equal(Money.Of(8000m), warranty.Compensation.Amount);
        Assert.Equal("Owner", warranty.Compensation.AuthorizedBy);
        Assert.Equal(authorizedAt, warranty.Compensation.AuthorizedAt);
    }

    [Fact]
    public void AuthorizeCompensation_OnRegularWorkOrder_ThrowsDomainException()
    {
        var workOrder = AssignedWorkOrder();

        Assert.Throws<DomainException>(() =>
            workOrder.AuthorizeCompensation(Money.Of(8000m), "Owner", Now.AddDays(1)));
    }

    [Fact]
    public void AuthorizeCompensation_BeforeAssignment_ThrowsDomainException()
    {
        var warranty = NewWarranty();

        Assert.Throws<DomainException>(() =>
            warranty.AuthorizeCompensation(Money.Of(8000m), "Owner", Now.AddDays(1)));
    }

    [Fact]
    public void AuthorizeCompensation_ForTheOriginalTailor_ThrowsDomainException()
    {
        var originalTailor = new TailorId(Guid.NewGuid());
        var warranty = NewWarranty(WorkOrderId.New(), originalTailor);
        warranty.AssignTo(originalTailor);

        Assert.Throws<DomainException>(() =>
            warranty.AuthorizeCompensation(Money.Of(8000m), "Owner", Now.AddDays(1)));
    }

    [Fact]
    public void AuthorizeCompensation_Twice_ThrowsDomainException()
    {
        var warranty = WarrantyDoneByAnotherTailor();
        warranty.AuthorizeCompensation(Money.Of(8000m), "Owner", Now.AddDays(1));

        Assert.Throws<DomainException>(() =>
            warranty.AuthorizeCompensation(Money.Of(5000m), "Owner", Now.AddDays(2)));
    }

    [Fact]
    public void AuthorizeCompensation_UnlinkedWarranty_IsAllowed()
    {
        var warranty = NewWarranty();
        warranty.AssignTo(new TailorId(Guid.NewGuid()));

        warranty.AuthorizeCompensation(Money.Of(8000m), "Owner", Now.AddDays(1));

        Assert.NotNull(warranty.Compensation);
    }

    [Fact]
    public void AuthorizeCompensation_BeforeCreationDate_ThrowsDomainException()
    {
        var warranty = WarrantyDoneByAnotherTailor();

        Assert.Throws<DomainException>(() =>
            warranty.AuthorizeCompensation(Money.Of(8000m), "Owner", Now.AddHours(-1)));
    }

    [Fact]
    public void Create_WithoutJobs_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            WorkOrder.Create(SerialNumber.From(1), "Maria Lopez", Phone, "Black pants", [], Now));
    }

    [Fact]
    public void CreateWarranty_WithoutJobs_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            WorkOrder.CreateWarranty(SerialNumber.From(2), "Pedro Gomez", Phone, "Black pants", [], Now));
    }

    [Fact]
    public void Create_RaisesWorkOrderCreated()
    {
        var workOrder = NewWorkOrder();

        var created = Assert.IsType<WorkOrderCreated>(Assert.Single(workOrder.DomainEvents));
        Assert.Equal(workOrder.Id, created.WorkOrderId);
        Assert.Equal(SerialNumber.From(1), created.SerialNumber);
        Assert.Equal(WorkOrderType.Regular, created.Type);
        Assert.Null(created.OriginalWorkOrderId);
        Assert.Equal(Now, created.CreatedAt);
    }

    [Fact]
    public void CreateWarranty_RaisesWorkOrderCreatedWithTheOriginalLink()
    {
        var originalId = WorkOrderId.New();

        var warranty = NewWarranty(originalId, new TailorId(Guid.NewGuid()));

        var created = Assert.IsType<WorkOrderCreated>(Assert.Single(warranty.DomainEvents));
        Assert.Equal(WorkOrderType.Warranty, created.Type);
        Assert.Equal(originalId, created.OriginalWorkOrderId);
    }

    [Fact]
    public void AssignTo_RaisesTailorAssigned()
    {
        var tailorId = new TailorId(Guid.NewGuid());
        var workOrder = NewWorkOrder();

        workOrder.AssignTo(tailorId);

        var assigned = workOrder.DomainEvents.OfType<TailorAssigned>().Single();
        Assert.Equal(workOrder.Id, assigned.WorkOrderId);
        Assert.Equal(tailorId, assigned.TailorId);
        Assert.Null(assigned.ReassignmentReason);
    }

    [Fact]
    public void SetPrice_RaisesPriceSet()
    {
        var workOrder = NewWorkOrder();

        workOrder.SetPrice(Money.Of(25000m));

        var priceSet = workOrder.DomainEvents.OfType<PriceSet>().Single();
        Assert.Equal(Money.Of(25000m), priceSet.Price);
    }

    [Fact]
    public void RegisterPayment_RaisesPaymentReceivedWithTheNewStatus()
    {
        var workOrder = PricedWorkOrder(30000m);
        var paymentId = PaymentId.New();

        workOrder.RegisterPayment(paymentId, Money.Of(10000m), Now);

        var received = workOrder.DomainEvents.OfType<PaymentReceived>().Single();
        Assert.Equal(paymentId, received.PaymentId);
        Assert.Equal(Money.Of(10000m), received.Amount);
        Assert.Equal(PaymentStatus.PartiallyPaid, received.PaymentStatus);
        Assert.Equal(Now, received.PaidAt);
    }

    [Fact]
    public void RegisterPayment_SamePaymentIdTwice_RaisesTheEventOnlyOnce()
    {
        var workOrder = PricedWorkOrder(30000m);
        var paymentId = PaymentId.New();

        workOrder.RegisterPayment(paymentId, Money.Of(10000m), Now);
        workOrder.RegisterPayment(paymentId, Money.Of(10000m), Now);

        Assert.Single(workOrder.DomainEvents.OfType<PaymentReceived>());
    }

    [Fact]
    public void MarkDeliveredByTailor_RaisesGarmentDeliveredByTailor()
    {
        var tailorId = new TailorId(Guid.NewGuid());
        var workOrder = NewWorkOrder();
        workOrder.AssignTo(tailorId);
        var deliveredAt = Now.AddDays(2);

        workOrder.MarkDeliveredByTailor(deliveredAt);

        var delivered = workOrder.DomainEvents.OfType<GarmentDeliveredByTailor>().Single();
        Assert.Equal(tailorId, delivered.TailorId);
        Assert.Equal(WorkOrderType.Regular, delivered.Type);
        Assert.Equal(deliveredAt, delivered.DeliveredAt);
    }

    [Fact]
    public void DeliverToCustomer_RaisesGarmentDeliveredToCustomer()
    {
        var workOrder = AssignedWorkOrder();
        workOrder.SetPrice(Money.Of(30000m));
        workOrder.RegisterPayment(PaymentId.New(), Money.Of(30000m), Now.AddDays(1));
        workOrder.MarkDeliveredByTailor(Now.AddDays(2));
        var deliveredAt = Now.AddDays(3);

        workOrder.DeliverToCustomer(deliveredAt);

        var delivered = workOrder.DomainEvents.OfType<GarmentDeliveredToCustomer>().Single();
        Assert.Equal(deliveredAt, delivered.DeliveredAt);
    }

    [Fact]
    public void AuthorizeCompensation_RaisesWarrantyCompensationAuthorized()
    {
        var warranty = WarrantyDoneByAnotherTailor();
        var authorizedAt = Now.AddDays(3);

        warranty.AuthorizeCompensation(Money.Of(8000m), "Owner", authorizedAt);

        var authorized = warranty.DomainEvents.OfType<WarrantyCompensationAuthorized>().Single();
        Assert.Equal(warranty.TailorId, authorized.TailorId);
        Assert.Equal(Money.Of(8000m), authorized.Amount);
        Assert.Equal("Owner", authorized.AuthorizedBy);
        Assert.Equal(authorizedAt, authorized.AuthorizedAt);
    }

    [Fact]
    public void WhenAnOperationFails_NoEventIsRaised()
    {
        var workOrder = AssignedWorkOrder();
        workOrder.ClearDomainEvents();

        Assert.Throws<DomainException>(() => workOrder.AssignTo(new TailorId(Guid.NewGuid())));

        Assert.Empty(workOrder.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var workOrder = NewWorkOrder();

        workOrder.ClearDomainEvents();

        Assert.Empty(workOrder.DomainEvents);
    }
}
