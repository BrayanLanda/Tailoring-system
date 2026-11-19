using IdentityService.Domain.Entities;
using IdentityService.Domain.Events;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Domain.Tests.TailorInvitations;

public sealed class TailorInvitationTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly InvitationTokenHash CorrectHash = InvitationTokenHash.Of("hash");
    private static readonly InvitationTokenHash NewHash = InvitationTokenHash.Of("new-hash");
    private static readonly EmailAddress AnaEmail = EmailAddress.Of("ana@example.com");

    // ---------- Create ----------

    [Fact]
    public void Create_ValidInvitation_StartsPending()
    {
        var tailorProfileId = new TailorProfileId(Guid.CreateVersion7());

        var invitation = TailorInvitation.Create(
            AnaEmail, tailorProfileId, CorrectHash, IssuedAt, IssuedAt.AddDays(2));

        Assert.Equal("ana@example.com", invitation.Email.Value);
        Assert.Equal(tailorProfileId, invitation.TailorProfileId);
        Assert.Equal(IssuedAt.AddDays(2), invitation.ExpiresAt);
        Assert.Null(invitation.AcceptedAt);
        Assert.Single(invitation.DomainEvents);
    }

    [Fact]
    public void Create_EmptyTailorProfileId_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => TailorInvitation.Create(
            AnaEmail, new TailorProfileId(Guid.Empty), CorrectHash, IssuedAt, IssuedAt.AddDays(1)));
    }

    [Fact]
    public void Create_ExpirationNotAfterIssue_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => TailorInvitation.Create(
            AnaEmail, new TailorProfileId(Guid.CreateVersion7()), CorrectHash, IssuedAt, IssuedAt));
    }

    // ---------- Accept ----------

    [Fact]
    public void Accept_BeforeExpiration_MarksInvitationAccepted()
    {
        var invitation = CreateInvitation();
        var acceptedAt = IssuedAt.AddHours(1);

        invitation.Accept(CorrectHash, acceptedAt);

        Assert.Equal(acceptedAt, invitation.AcceptedAt);
        Assert.Equal(2, invitation.DomainEvents.Count);
    }

    [Fact]
    public void Accept_AtOrAfterExpiration_ThrowsAndDoesNotAccept()
    {
        var invitation = CreateInvitation();

        Assert.Throws<DomainException>(() => invitation.Accept(CorrectHash, invitation.ExpiresAt));

        Assert.Null(invitation.AcceptedAt);
        Assert.Single(invitation.DomainEvents);
    }

    [Fact]
    public void Accept_AlreadyAcceptedInvitation_ThrowsDomainException()
    {
        var invitation = CreateInvitation();
        invitation.Accept(CorrectHash, IssuedAt.AddHours(1));

        Assert.Throws<DomainException>(() => invitation.Accept(CorrectHash, IssuedAt.AddHours(2)));
    }

    [Fact]
    public void Accept_WithAWrongToken_ThrowsAndDoesNotAccept()
    {
        var invitation = CreateInvitation();

        Assert.Throws<DomainException>(() =>
            invitation.Accept(InvitationTokenHash.Of("another-hash"), IssuedAt.AddHours(1)));

        Assert.Null(invitation.AcceptedAt);
        Assert.Single(invitation.DomainEvents);
    }

    [Fact]
    public void Accept_WithAWrongToken_GivesTheSameAnswerWhateverTheStateOfTheInvitation()
    {
        var invitation = CreateInvitation();
        var wrong = InvitationTokenHash.Of("another-hash");

        var duringValidity = Assert.Throws<DomainException>(() =>
            invitation.Accept(wrong, IssuedAt.AddHours(1)));
        var afterExpiry = Assert.Throws<DomainException>(() =>
            invitation.Accept(wrong, invitation.ExpiresAt.AddDays(1)));

        // Someone without the token must not learn whether the invitation expired.
        Assert.Equal(duringValidity.Message, afterExpiry.Message);
    }

    // ---------- Reissue ----------

    [Fact]
    public void Reissue_ReplacesTheTokenHash_SoTheOldTokenNoLongerAccepts()
    {
        var invitation = CreateInvitation();

        invitation.Reissue(AnaEmail, NewHash, IssuedAt.AddHours(2), IssuedAt.AddDays(2));

        Assert.Throws<DomainException>(() => invitation.Accept(CorrectHash, IssuedAt.AddHours(3)));
        Assert.Null(invitation.AcceptedAt);
    }

    [Fact]
    public void Reissue_TheNewTokenCanBeAccepted()
    {
        var invitation = CreateInvitation();
        invitation.Reissue(AnaEmail, NewHash, IssuedAt.AddHours(2), IssuedAt.AddDays(2));

        invitation.Accept(NewHash, IssuedAt.AddHours(3));

        Assert.Equal(IssuedAt.AddHours(3), invitation.AcceptedAt);
    }

    [Fact]
    public void Reissue_AnExpiredInvitation_StartsANewValidityPeriod()
    {
        var invitation = CreateInvitation();              // it expires one day after IssuedAt
        var reissuedAt = IssuedAt.AddDays(3);             // so it is long expired by now

        invitation.Reissue(AnaEmail, NewHash, reissuedAt, reissuedAt.AddDays(1));
        invitation.Accept(NewHash, reissuedAt.AddHours(1));

        Assert.NotNull(invitation.AcceptedAt);
    }

    [Fact]
    public void Reissue_CanChangeTheEmail()
    {
        var invitation = CreateInvitation();

        invitation.Reissue(
            EmailAddress.Of("  Ana.New@Example.com "), NewHash, IssuedAt.AddHours(1), IssuedAt.AddDays(2));

        Assert.Equal("ana.new@example.com", invitation.Email.Value);
    }

    [Fact]
    public void Reissue_AfterAcceptance_ThrowsDomainException()
    {
        var invitation = CreateInvitation();
        invitation.Accept(CorrectHash, IssuedAt.AddHours(1));

        Assert.Throws<DomainException>(() =>
            invitation.Reissue(AnaEmail, NewHash, IssuedAt.AddHours(2), IssuedAt.AddDays(2)));
    }

    [Fact]
    public void Reissue_ExpirationNotAfterIssue_ThrowsAndKeepsTheCurrentInvitation()
    {
        var invitation = CreateInvitation();

        Assert.Throws<DomainException>(() =>
            invitation.Reissue(AnaEmail, NewHash, IssuedAt.AddHours(2), IssuedAt.AddHours(2)));

        // The original token still works: a rejected reissue changed nothing.
        invitation.Accept(CorrectHash, IssuedAt.AddHours(3));
        Assert.NotNull(invitation.AcceptedAt);
    }

    [Fact]
    public void Reissue_RaisesTailorInvitationReissuedWithTheSameInvitationId()
    {
        var invitation = CreateInvitation();

        invitation.Reissue(AnaEmail, NewHash, IssuedAt.AddHours(2), IssuedAt.AddDays(2));

        var reissued = invitation.DomainEvents.OfType<TailorInvitationReissued>().Single();
        Assert.Equal(invitation.Id, reissued.InvitationId);
        Assert.Equal(IssuedAt.AddDays(2), reissued.ExpiresAt);
    }

    private static TailorInvitation CreateInvitation() => TailorInvitation.Create(
        AnaEmail,
        new TailorProfileId(Guid.CreateVersion7()),
        CorrectHash,
        IssuedAt,
        IssuedAt.AddDays(1));
}
