using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TailorService.Domain.ValueObjects;

namespace TailorService.Infrastructure.Persistence;

internal sealed class TailorIdConverter()
    : ValueConverter<TailorId, Guid>(id => id.Value, value => new TailorId(value));
