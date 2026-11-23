using Microsoft.EntityFrameworkCore;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Tailors;

namespace WorkOrderService.Infrastructure.Persistence.Repositories;

public sealed class EfAssignableTailorRepository(WorkOrderDbContext dbContext) : IAssignableTailorRepository
{
    public async Task<AssignableTailor?> GetByIdAsync(Guid tailorId, CancellationToken cancellationToken = default)
    {
        var projection = await dbContext.AssignableTailors
            .AsNoTracking()
            .SingleOrDefaultAsync(tailor => tailor.TailorId == tailorId, cancellationToken);

        return projection is null
            ? null
            : new AssignableTailor(projection.TailorId, projection.IsActive, projection.LastOccurredOn);
    }
}
