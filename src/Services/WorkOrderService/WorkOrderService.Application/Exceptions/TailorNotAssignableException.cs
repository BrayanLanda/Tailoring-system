namespace WorkOrderService.Application.Exceptions;

public sealed class TailorNotAssignableException(Guid tailorId)
    : Exception($"Tailor '{tailorId}' does not exist or is inactive.");
