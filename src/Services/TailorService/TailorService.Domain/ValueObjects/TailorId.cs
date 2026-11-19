namespace TailorService.Domain.ValueObjects;

public readonly record struct TailorId(Guid Value)
{
    public static TailorId New() => new(Guid.CreateVersion7());
}
