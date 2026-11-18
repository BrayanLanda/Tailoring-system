using WorkOrderService.Domain.Enums;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.WorkOrders.RegisterPayment;

public sealed record RegisterPaymentResult(
    WorkOrderId WorkOrderId,
    PaymentId PaymentId,
    PaymentStatus PaymentStatus);
