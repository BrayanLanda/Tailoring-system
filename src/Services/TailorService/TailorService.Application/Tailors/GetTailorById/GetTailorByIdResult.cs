using TailorService.Domain.ValueObjects;

namespace TailorService.Application.Tailors.GetTailorById;

public sealed record GetTailorByIdResult(TailorId TailorId, string Name, bool IsActive);
