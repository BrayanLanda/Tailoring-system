using WorkOrderService.Domain.Enums;
using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Entities;

public class WorkOrder
{

    private readonly List<Payment> _payments = [];

    public WorkOrderId Id { get; private set; }
    public SerialNumber SerialNumber { get; private set; } = null!;
    public string CustomerName { get; private set; } = null!;
    public PhoneNumber CustomerPhone { get; private set; } = null!;
    public string GarmentDescription { get; private set; } = null!;
    public WorkOrderType Type { get; private set; }
    public WorkStatus Status { get; private set; }
    public TailorId? TailorId { get; private set; }
    public Money? Price { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeliveredByTailorAt { get; private set; }
    public DateTimeOffset? DeliveredToCustomerAt { get; private set; }

    // Warranty traceability
    public WorkOrderId? OriginalWorkOrderId { get; private set; }
    public TailorId? OriginalTailorId { get; private set; }
    public string? ReassignmentReason { get; private set; }
    public bool IsLinkedToOriginal => OriginalWorkOrderId is not null;

    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();
    public Money AmountPaid => Money.Of(_payments.Sum(p => p.Amount.Amount));
    public Money? Balance => Price is null ? null : Price - AmountPaid;

    // Used by EF Core only. Nobody outside can build an invalid WorkOrder.
    private WorkOrder() { }

    public static WorkOrder Create(
        SerialNumber serialNumber,
        string customerName,
        PhoneNumber customerPhone,
        string garmentDescription,
        DateTimeOffset createdAt) =>
        Build(WorkOrderType.Regular, PaymentStatus.Unpriced,
              serialNumber, customerName, customerPhone, garmentDescription, createdAt);

    public static WorkOrder CreateWarranty(
        SerialNumber serialNumber,
        string customerName,
        PhoneNumber customerPhone,
        string garmentDescription,
        DateTimeOffset createdAt,
        WorkOrderId? originalWorkOrderId = null,
        TailorId? originalTailorId = null)
    {
        if (originalTailorId is not null && originalWorkOrderId is null)
            throw new DomainException("The original tailor cannot be known without the original work order.");

        var warranty = Build(WorkOrderType.Warranty, PaymentStatus.NoCharge,
                             serialNumber, customerName, customerPhone, garmentDescription, createdAt);

        warranty.OriginalWorkOrderId = originalWorkOrderId;
        warranty.OriginalTailorId = originalTailorId;
        return warranty;
    }

    private static WorkOrder Build(
        WorkOrderType type,
        PaymentStatus paymentStatus,
        SerialNumber serialNumber,
        string customerName,
        PhoneNumber customerPhone,
        string garmentDescription,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new DomainException("Customer name is required.");

        if (string.IsNullOrWhiteSpace(garmentDescription))
            throw new DomainException("Garment description is required.");

        return new WorkOrder
        {
            Id = WorkOrderId.New(),
            SerialNumber = serialNumber,
            CustomerName = customerName.Trim(),
            CustomerPhone = customerPhone,
            GarmentDescription = garmentDescription.Trim(),
            Type = type,
            Status = WorkStatus.Received,
            PaymentStatus = paymentStatus,
            CreatedAt = createdAt
        };
    }

    public void AssignTo(TailorId tailorId, string? reassignmentReason = null)
    {
        if (Status != WorkStatus.Received)
            throw new DomainException($"Cannot assign a work order in status {Status}.");

        if (Type == WorkOrderType.Warranty
            && OriginalTailorId is { } originalTailor
            && originalTailor != tailorId)
        {
            if (string.IsNullOrWhiteSpace(reassignmentReason))
                throw new DomainException(
                    "A warranty assigned to a different tailor than the original requires a reason.");

            ReassignmentReason = reassignmentReason.Trim();
        }

        TailorId = tailorId;
        Status = WorkStatus.Assigned;
    }

    public void StartWork()
    {
        if (Status != WorkStatus.Assigned)
            throw new DomainException($"Cannot start work on a work order in status {Status}.");

        Status = WorkStatus.InProgress;
    }

    public void MarkDeliveredByTailor(DateTimeOffset deliveredAt)
    {
        if (Status is not (WorkStatus.Assigned or WorkStatus.InProgress))
            throw new DomainException($"The tailor cannot deliver a work order in status {Status}.");

        if (deliveredAt < CreatedAt)
            throw new DomainException("Delivery date cannot be before the creation date.");

        DeliveredByTailorAt = deliveredAt;
        Status = WorkStatus.DeliveredByTailor;
    }

    public void SetPrice(Money price)
    {
        if (Type == WorkOrderType.Warranty)
            throw new DomainException("Warranty work orders have no charge.");

        if (PaymentStatus is PaymentStatus.PartiallyPaid or PaymentStatus.Paid)
            throw new DomainException("The price cannot be changed once payments have been registered.");

        if (price.Amount <= 0)
            throw new DomainException("Price must be greater than zero.");

        Price = price;
        PaymentStatus = PaymentStatus.Priced;
    }

    public void RegisterPayment(PaymentId paymentId, Money amount, DateTimeOffset paidAt)
    {
        if (Type == WorkOrderType.Warranty)
            throw new DomainException("Warranty work orders have no charge.");

        // Idempotent: a retried request with the same id is ignored.
        if (_payments.Any(p => p.Id == paymentId))
            return;

        var price = Price ?? throw new DomainException("Cannot register a payment on a work order without a price.");

        if (amount.Amount <= 0)
            throw new DomainException("Payment amount must be greater than zero.");

        if (paidAt < CreatedAt)
            throw new DomainException("Payment date cannot be before the creation date.");

        if (amount.Amount > (price - AmountPaid).Amount)
            throw new DomainException("Payment exceeds the pending balance.");

        _payments.Add(Payment.Create(paymentId, amount, paidAt));

        PaymentStatus = AmountPaid.Amount == price.Amount
            ? PaymentStatus.Paid
            : PaymentStatus.PartiallyPaid;
    }

    public void DeliverToCustomer(DateTimeOffset deliveredAt)
    {
        if (Status != WorkStatus.DeliveredByTailor)
            throw new DomainException($"Cannot deliver to the customer a work order in status {Status}.");

        if (PaymentStatus is not (PaymentStatus.Paid or PaymentStatus.NoCharge))
            throw new DomainException("The work order must be fully paid before delivering it to the customer.");

        if (deliveredAt < DeliveredByTailorAt)
            throw new DomainException("Customer delivery cannot be before the tailor delivery.");

        DeliveredToCustomerAt = deliveredAt;
        Status = WorkStatus.DeliveredToCustomer;
    }
}
