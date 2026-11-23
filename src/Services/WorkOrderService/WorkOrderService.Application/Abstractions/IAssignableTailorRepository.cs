using WorkOrderService.Application.Tailors;

namespace WorkOrderService.Application.Abstractions;

public interface IAssignableTailorRepository
{
    Task<AssignableTailor?> GetByIdAsync(Guid tailorId, CancellationToken cancellationToken = default);
}
