using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.WorkOrders.AssignTailor;

public sealed record AssignTailorCommand(
    WorkOrderId WorkOrderId,
    Guid TailorId,
    string? ReassignmentReason = null);
