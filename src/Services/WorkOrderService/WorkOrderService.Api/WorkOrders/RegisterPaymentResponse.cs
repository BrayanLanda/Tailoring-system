namespace WorkOrderService.Api.WorkOrders;

public sealed record RegisterPaymentResponse(Guid WorkOrderId, Guid PaymentId, string PaymentStatus);
