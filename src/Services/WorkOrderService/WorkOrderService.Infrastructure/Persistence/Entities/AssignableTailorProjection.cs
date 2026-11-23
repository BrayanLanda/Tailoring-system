namespace WorkOrderService.Infrastructure.Persistence.Entities;

public sealed class AssignableTailorProjection
{
    private AssignableTailorProjection() { }

    public Guid TailorId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset LastOccurredOn { get; private set; }

    public static AssignableTailorProjection Create(Guid tailorId, bool isActive, DateTimeOffset occurredOn) => new()
    {
        TailorId = tailorId,
        IsActive = isActive,
        LastOccurredOn = occurredOn
    };

    public bool ApplyState(bool isActive, DateTimeOffset occurredOn)
    {
        if (occurredOn <= LastOccurredOn)
            return false;

        IsActive = isActive;
        LastOccurredOn = occurredOn;
        return true;
    }
}
