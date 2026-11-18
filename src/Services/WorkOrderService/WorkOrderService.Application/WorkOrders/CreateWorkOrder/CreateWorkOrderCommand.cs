namespace WorkOrderService.Application.WorkOrders.CreateWorkOrder;

public sealed record CreateWorkOrderCommand(
    string CustomerName,
    string CustomerPhone,
    string GarmentDescription,
    IReadOnlyCollection<CreateWorkOrderJob> Jobs);

public sealed record CreateWorkOrderJob(string Description, int Quantity);
