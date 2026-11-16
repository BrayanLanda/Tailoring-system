namespace WorkOrderService.Domain.Enums;

public enum WorkStatus
{
    Received = 1,
    Assigned = 2,
    InProgress = 3,
    DeliveredByTailor = 4,
    DeliveredToCustomer = 5
}
