using WorkOrderService.Domain.Enums;
using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Entities;

public class WorkOrder
{
    public WorkOrderId Id { get; private set; }
    public SerialNumber SerialNumber { get; private set; } = null!;
    public string CustomerName { get; private set; } = null!;
    public string GarmentDescription { get; private set; } = null!;
    public WorkOrderType Type { get; private set; }
    public WorkStatus Status { get; private set; }
    public TailorId? TailorId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private WorkOrder() {}

    public static WorkOrder Create(
        SerialNumber serialNumber,
        string customerName,
        string garmenDescription,   
        DateTimeOffset createdAt)
    {
        if(string.IsNullOrWhiteSpace(customerName))
            throw new DomainException("Customer name is required");

        if(string.IsNullOrWhiteSpace(garmenDescription))
            throw new DomainException("Garment description is required");
        
        return new WorkOrder
        {
            Id = WorkOrderId.New(),
            SerialNumber = serialNumber,
            CustomerName = customerName.Trim(),
            GarmentDescription = garmenDescription.Trim(),
            Type = WorkOrderType.Regular,
            Status = WorkStatus.Received,
            CreatedAt = createdAt
        };
    }

    public void AssignTo(TailorId tailorId)
    {
        if(Status != WorkStatus.Received)
            throw new DomainException($"Cannot assign a work order in status {Status}");

        TailorId = tailorId;
        Status = WorkStatus.Assigned;
    }
}
