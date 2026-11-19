namespace WorkOrderService.Api.WorkOrders;

public sealed record RegisterPaymentRequest(Guid PaymentId, decimal Amount);
