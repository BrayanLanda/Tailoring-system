using TailorService.Domain.Common;
using TailorService.Domain.ValueObjects;

namespace TailorService.Domain.Events;

public sealed record TailorRegistered(TailorId TailorId, string Name) : IDomainEvent;
public sealed record TailorDeactivated(TailorId TailorId) : IDomainEvent;
public sealed record TailorReactivated(TailorId TailorId) : IDomainEvent;