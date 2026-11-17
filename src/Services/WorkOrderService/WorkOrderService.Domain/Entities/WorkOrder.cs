using WorkOrderService.Domain.Enums;
using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Entities;

public class WorkOrder
{
    public WorkOrderId Id { get; private set; }
    public SerialNumber SerialNumber { get; private set; } = null!;
    public string CustomerName { get; private set; } = null!;
    public PhoneNumber CustomerPhone { get; private set; } = null!;
    public string GarmentDescription { get; private set; } = null!;
    public WorkOrderType Type { get; private set; }
    public WorkStatus Status { get; private set; }
    public TailorId? TailorId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeliveredByTailorAt { get; private set; }
    public DateTimeOffset? DeliveredToCustomerAt { get; private set; }
    public Money? Price { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }

    private WorkOrder() { }

    public static WorkOrder Create(
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
            Type = WorkOrderType.Regular,
            Status = WorkStatus.Received,
            CreatedAt = createdAt,
            PaymentStatus = PaymentStatus.Unpriced,
        };
    }

    public void AssignTo(TailorId tailorId)
    {
        if (Status != WorkStatus.Received)
            throw new DomainException($"Cannot assign a work order in status {Status}.");

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

    public void DeliverToCustomer(DateTimeOffset deliveredAt)
    {
        if (Status != WorkStatus.DeliveredByTailor)
            throw new DomainException($"Cannot deliver to the customer a work order in status {Status}.");

        if (deliveredAt < DeliveredByTailorAt)
            throw new DomainException("Customer delivery cannot be before the tailor delivery.");

        DeliveredToCustomerAt = deliveredAt;
        Status = WorkStatus.DeliveredToCustomer;
    }

    public void SetPrice(Money price)
    {
        if(price.Amount <= 0)
            throw new DomainException("Price must be greater than zero");

        Price = price;
        PaymentStatus = PaymentStatus.Priced;
    }

}
