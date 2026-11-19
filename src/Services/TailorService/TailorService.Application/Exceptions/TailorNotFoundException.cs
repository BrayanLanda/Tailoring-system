using TailorService.Domain.ValueObjects;

namespace TailorService.Application.Exceptions;

public sealed class TailorNotFoundException(TailorId id)
    : Exception($"Tailor '{id.Value}' was not found.");
