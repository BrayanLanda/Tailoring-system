using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.WorkOrders.RegisterPayment;

public sealed record RegisterPaymentCommand(WorkOrderId WorkOrderId, PaymentId PaymentId, decimal Amount);
