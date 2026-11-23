using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.WorkOrders.AssignTailor;

public sealed record AssignTailorResult(WorkOrderId WorkOrderId, Guid TailorId);
