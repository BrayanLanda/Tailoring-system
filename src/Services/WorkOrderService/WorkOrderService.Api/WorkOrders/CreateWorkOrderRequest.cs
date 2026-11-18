namespace WorkOrderService.Api.WorkOrders;

public sealed record CreateWorkOrderRequest(
    string CustomerName,
    string CustomerPhone,
    string GarmentDescription,
    IReadOnlyCollection<CreateWorkOrderJobRequest?>? Jobs);

public sealed record CreateWorkOrderJobRequest(string Description, int Quantity);
