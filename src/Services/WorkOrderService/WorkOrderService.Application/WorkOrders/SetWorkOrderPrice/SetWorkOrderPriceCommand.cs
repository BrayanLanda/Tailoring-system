using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.WorkOrders.SetWorkOrderPrice;

public sealed record SetWorkOrderPriceCommand(WorkOrderId WorkOrderId, decimal Amount);
