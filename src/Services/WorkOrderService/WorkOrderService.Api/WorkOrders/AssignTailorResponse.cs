namespace WorkOrderService.Api.WorkOrders;

public sealed record AssignTailorResponse(Guid WorkOrderId, Guid TailorId);
