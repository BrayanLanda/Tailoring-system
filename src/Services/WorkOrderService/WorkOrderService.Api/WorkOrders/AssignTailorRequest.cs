namespace WorkOrderService.Api.WorkOrders;

public sealed record AssignTailorRequest(Guid TailorId, string? ReassignmentReason = null);
