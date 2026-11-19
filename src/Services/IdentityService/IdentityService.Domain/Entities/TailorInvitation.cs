using IdentityService.Domain.Common;
using IdentityService.Domain.Events;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Domain.Entities;

public sealed class TailorInvitation : AggregateRoot
{
    private TailorInvitation() { }

    public TailorInvitationId Id { get; private set; }
    public EmailAddress Email { get; private set; }
    public TailorProfileId TailorProfileId { get; private set; }
    public InvitationTokenHash TokenHash { get; private set; } = null!;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }

    public static TailorInvitation Create(
        string email,
        TailorProfileId tailorProfileId,
        InvitationTokenHash tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        var normalizedEmail = EmailAddress.Of(email);

        if (tailorProfileId.Value == Guid.Empty)
            throw new DomainException("A tailor profile ID is required for the invitation.");

        if (expiresAt <= issuedAt)
            throw new DomainException("The invitation must expire after it is issued.");

        var invitation = new TailorInvitation
        {
            Id = TailorInvitationId.New(),
            Email = normalizedEmail,
            TailorProfileId = tailorProfileId,
            TokenHash = tokenHash,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt
        };

        invitation.Raise(new TailorInvitationIssued(
            invitation.Id,
            invitation.TailorProfileId,
            invitation.Email.Value,
            invitation.ExpiresAt));

        return invitation;
    }

    public void Accept(InvitationTokenHash presentedTokenHash, DateTimeOffset acceptedAt)
    {
        if (!TokenHash.Matches(presentedTokenHash))
            throw new DomainException("The invitation is not valid.");

        if (AcceptedAt is not null)
            throw new DomainException("The invitation has already been accepted.");

        if (acceptedAt < IssuedAt)
            throw new DomainException("The invitation cannot be accepted before it is issued.");

        if (acceptedAt >= ExpiresAt)
            throw new DomainException("The invitation has expired.");

        AcceptedAt = acceptedAt;
        Raise(new TailorInvitationAccepted(Id, TailorProfileId, acceptedAt));
    }

    public void Reissue(
        EmailAddress email,
        InvitationTokenHash tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        if (AcceptedAt is not null)
            throw new DomainException("The invitation has already been accepted; the tailor already has access.");

        if (expiresAt <= issuedAt)
            throw new DomainException("The invitation must expire after it is issued.");

        Email = email;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;

        Raise(new TailorInvitationReissued(Id, TailorProfileId, Email.Value, ExpiresAt));
    }
}
