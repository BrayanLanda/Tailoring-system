using TailorService.Domain.ValueObjects;

namespace TailorService.Application.Tailors.GetTailorById;

public sealed record GetTailorByIdQuery(TailorId TailorId);
