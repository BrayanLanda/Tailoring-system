using IdentityService.Domain.Common;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Domain.Events;

public sealed record TailorInvitationIssued(
    TailorInvitationId InvitationId,
    TailorProfileId TailorProfileId,
    string Email,
    DateTimeOffset ExpiresAt) : IDomainEvent;

public sealed record TailorInvitationAccepted(
    TailorInvitationId InvitationId,
    TailorProfileId TailorProfileId,
    DateTimeOffset AcceptedAt) : IDomainEvent;

public sealed record TailorInvitationReissued(
    TailorInvitationId InvitationId,
    TailorProfileId TailorProfileId,
    string Email,
    DateTimeOffset ExpiresAt) : IDomainEvent;
