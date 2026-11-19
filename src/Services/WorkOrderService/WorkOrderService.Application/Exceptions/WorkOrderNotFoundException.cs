using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.Exceptions;

public sealed class WorkOrderNotFoundException(WorkOrderId id)
    : Exception($"Work order '{id.Value}' was not found.");
