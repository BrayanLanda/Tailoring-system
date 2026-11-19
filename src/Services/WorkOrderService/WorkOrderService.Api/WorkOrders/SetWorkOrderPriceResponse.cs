namespace WorkOrderService.Api.WorkOrders;

public sealed record SetWorkOrderPriceResponse(Guid WorkOrderId, decimal Price);
