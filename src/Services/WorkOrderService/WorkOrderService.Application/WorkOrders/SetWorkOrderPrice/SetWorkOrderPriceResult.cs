using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.WorkOrders.SetWorkOrderPrice;

public sealed record SetWorkOrderPriceResult(WorkOrderId WorkOrderId, Money Price);
