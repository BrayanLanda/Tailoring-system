using TailorService.Domain.ValueObjects;

namespace TailorService.Application.Tailors.RegisterTailor;

public sealed record RegisterTailorResult(TailorId TailorId, string Name, bool IsActive);
