using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Domain.Entities;
using WorkOrderService.Domain.Enums;
using WorkOrderService.Domain.Repositories;
using WorkOrderService.Domain.Services;
using WorkOrderService.Domain.ValueObjects;
using WorkOrderService.Application.Exceptions;

namespace WorkOrderService.Infrastructure.IntegrationTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class WorkOrderPersistenceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveAndReload_WorkOrder_KeepsAllTheData()
    {
        var id = await SaveAsync(serial => RegularWorkOrder(serial));

        await using var request = postgres.NewRequest();
        var workOrder = await Repository(request).GetByIdAsync(id);

        Assert.NotNull(workOrder);
        Assert.Equal("Maria Lopez", workOrder.CustomerName);
        Assert.Equal("3001234567", workOrder.CustomerPhone.Value);
        Assert.Equal("Black pants", workOrder.GarmentDescription);
        Assert.Equal(WorkOrderType.Regular, workOrder.Type);
        Assert.Equal(WorkStatus.Received, workOrder.Status);
        Assert.Equal(PaymentStatus.Unpriced, workOrder.PaymentStatus);
        Assert.Equal(Now, workOrder.CreatedAt);
        Assert.Null(workOrder.Price);
        Assert.Equal(2, workOrder.Jobs.Count);
        Assert.Contains(AlterationJob.Of("Hem", 3), workOrder.Jobs);
        Assert.Contains(AlterationJob.Of("Seam", 2), workOrder.Jobs);
    }

    [Fact]
    public async Task RegisterPayment_OnReloadedWorkOrder_InsertsThePayment()
    {
        var id = await SaveAsync(serial => RegularWorkOrder(serial, price: 30000m));
        var paymentId = PaymentId.New();

        await RegisterPaymentInNewRequestAsync(id, paymentId, 10000m);

        await using var verify = postgres.NewRequest();
        var reloaded = await Repository(verify).GetByIdAsync(id);
        Assert.NotNull(reloaded);
        var payment = Assert.Single(reloaded.Payments);
        Assert.Equal(paymentId, payment.Id);
        Assert.Equal(Money.Of(10000m), payment.Amount);
        Assert.Equal(PaymentStatus.PartiallyPaid, reloaded.PaymentStatus);
    }

    [Fact]
    public async Task TwoPaymentsInSeparateRequests_BothArePersisted()
    {
        var id = await SaveAsync(serial => RegularWorkOrder(serial, price: 30000m));

        await RegisterPaymentInNewRequestAsync(id, PaymentId.New(), 10000m);
        await RegisterPaymentInNewRequestAsync(id, PaymentId.New(), 20000m);

        await using var verify = postgres.NewRequest();
        var reloaded = await Repository(verify).GetByIdAsync(id);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.Payments.Count);
        Assert.Equal(Money.Of(30000m), reloaded.AmountPaid);
        Assert.Equal(PaymentStatus.Paid, reloaded.PaymentStatus);
    }

    [Fact]
    public async Task SaveAndReload_Warranty_KeepsLinkReasonAndCompensation()
    {
        var originalId = WorkOrderId.New();
        var originalTailor = new TailorId(Guid.NewGuid());
        var otherTailor = new TailorId(Guid.NewGuid());
        var authorizedAt = Now.AddDays(3);

        var id = await SaveAsync(serial =>
        {
            var warranty = WorkOrder.CreateWarranty(
                serial, "Pedro Gomez", PhoneNumber.Of("3109876543"),
                "Black pants - hem came undone", [AlterationJob.Of("Hem")], Now,
                originalId, originalTailor);
            warranty.AssignTo(otherTailor, "Original tailor is on vacation");
            warranty.AuthorizeCompensation(Money.Of(8000m), "Owner", authorizedAt);
            return warranty;
        });

        await using var request = postgres.NewRequest();
        var reloaded = await Repository(request).GetByIdAsync(id);

        Assert.NotNull(reloaded);
        Assert.Equal(WorkOrderType.Warranty, reloaded.Type);
        Assert.Equal(PaymentStatus.NoCharge, reloaded.PaymentStatus);
        Assert.True(reloaded.IsLinkedToOriginal);
        Assert.Equal(originalId, reloaded.OriginalWorkOrderId);
        Assert.Equal(originalTailor, reloaded.OriginalTailorId);
        Assert.Equal(otherTailor, reloaded.TailorId);
        Assert.Equal("Original tailor is on vacation", reloaded.ReassignmentReason);
        Assert.NotNull(reloaded.Compensation);
        Assert.Equal(Money.Of(8000m), reloaded.Compensation.Amount);
        Assert.Equal("Owner", reloaded.Compensation.AuthorizedBy);
        Assert.Equal(authorizedAt, reloaded.Compensation.AuthorizedAt);
    }

    [Fact]
    public async Task SavingTwoWorkOrdersWithTheSameSerial_IsRejectedByTheDatabase()
    {
        var serial = SerialNumber.From(900_001);
        await SaveAsync(s => RegularWorkOrder(s), serial);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            SaveAsync(s => RegularWorkOrder(s), serial));
    }

    [Fact]
    public async Task SerialNumbers_AreIncreasingAcrossRequests()
    {
        await using var first = postgres.NewRequest();
        await using var second = postgres.NewRequest();

        var a = await first.ServiceProvider.GetRequiredService<ISerialNumberGenerator>().GetNextAsync();
        var b = await second.ServiceProvider.GetRequiredService<ISerialNumberGenerator>().GetNextAsync();

        Assert.True(b.Value > a.Value);
    }

     [Fact]
    public async Task TwoConcurrentPaymentsThatExceedTheBalance_SecondOneIsRejected()
    {
        var id = await SaveAsync(serial => RegularWorkOrder(serial, price: 30000m));
        await RegisterPaymentInNewRequestAsync(id, PaymentId.New(), 10000m); // balance is now 20000

        // Two receptionists load the same state at the same time.
        await using var first = postgres.NewRequest();
        await using var second = postgres.NewRequest();
        var workOrderA = await Repository(first).GetByIdAsync(id);
        var workOrderB = await Repository(second).GetByIdAsync(id);
        Assert.NotNull(workOrderA);
        Assert.NotNull(workOrderB);

        workOrderA.RegisterPayment(PaymentId.New(), Money.Of(15000m), Now.AddDays(2));
        workOrderB.RegisterPayment(PaymentId.New(), Money.Of(15000m), Now.AddDays(2));

        await UnitOfWork(first).SaveChangesAsync();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => UnitOfWork(second).SaveChangesAsync());

        // The customer was never charged more than the price.
        await using var verify = postgres.NewRequest();
        var reloaded = await Repository(verify).GetByIdAsync(id);
        Assert.NotNull(reloaded);
        Assert.Equal(Money.Of(25000m), reloaded.AmountPaid);
    }

    // ---------- helpers ----------

    private static WorkOrder RegularWorkOrder(SerialNumber serial, decimal? price = null)
    {
        var workOrder = WorkOrder.Create(
            serial, "Maria Lopez", PhoneNumber.Of("300 123 4567"), "Black pants",
            [AlterationJob.Of("Hem", 3), AlterationJob.Of("Seam", 2)], Now);

        if (price is { } amount)
            workOrder.SetPrice(Money.Of(amount));

        return workOrder;
    }

    private async Task<WorkOrderId> SaveAsync(
        Func<SerialNumber, WorkOrder> create,
        SerialNumber? serial = null)
    {
        await using var request = postgres.NewRequest();

        var number = serial
            ?? await request.ServiceProvider.GetRequiredService<ISerialNumberGenerator>().GetNextAsync();
        var workOrder = create(number);

        await Repository(request).AddAsync(workOrder);
        await UnitOfWork(request).SaveChangesAsync();

        return workOrder.Id;
    }

    private async Task RegisterPaymentInNewRequestAsync(WorkOrderId id, PaymentId paymentId, decimal amount)
    {
        await using var request = postgres.NewRequest();

        var workOrder = await Repository(request).GetByIdAsync(id);
        Assert.NotNull(workOrder);
        workOrder.RegisterPayment(paymentId, Money.Of(amount), Now.AddDays(1));

        await UnitOfWork(request).SaveChangesAsync();
    }

    private static IWorkOrderRepository Repository(AsyncServiceScope request) =>
        request.ServiceProvider.GetRequiredService<IWorkOrderRepository>();

    private static IUnitOfWork UnitOfWork(AsyncServiceScope request) =>
        request.ServiceProvider.GetRequiredService<IUnitOfWork>();
}
