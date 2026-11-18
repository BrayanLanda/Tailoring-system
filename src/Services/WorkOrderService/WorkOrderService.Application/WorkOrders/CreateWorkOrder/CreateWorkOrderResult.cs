using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.WorkOrders.CreateWorkOrder;

public sealed record CreateWorkOrderResult(WorkOrderId Id, SerialNumber SerialNumber);
