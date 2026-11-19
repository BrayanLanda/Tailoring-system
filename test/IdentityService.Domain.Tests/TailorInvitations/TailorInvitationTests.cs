using IdentityService.Domain.Entities;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Domain.Tests.TailorInvitations;

public sealed class TailorInvitationTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly InvitationTokenHash CorrectHash = InvitationTokenHash.Of("hash");

    [Fact]
    public void Create_ValidInvitation_NormalizesEmailAndStartsPending()
    {
        var tailorProfileId = new TailorProfileId(Guid.CreateVersion7());

        var invitation = TailorInvitation.Create(
            "  Ana.Gomez@Example.com ",
            tailorProfileId,
            CorrectHash,
            IssuedAt,
            IssuedAt.AddDays(2));

        Assert.Equal("ana.gomez@example.com", invitation.Email.Value);
        Assert.Equal(tailorProfileId, invitation.TailorProfileId);
        Assert.Equal(IssuedAt.AddDays(2), invitation.ExpiresAt);
        Assert.Null(invitation.AcceptedAt);
        Assert.Single(invitation.DomainEvents);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Create_InvalidEmail_ThrowsDomainException(string email)
    {
        Assert.Throws<DomainException>(() => TailorInvitation.Create(
            email,
            new TailorProfileId(Guid.CreateVersion7()),
            CorrectHash,
            IssuedAt,
            IssuedAt.AddDays(1)));
    }

    [Fact]
    public void Create_ExpirationNotAfterIssue_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => TailorInvitation.Create(
            "ana@example.com",
            new TailorProfileId(Guid.CreateVersion7()),
            CorrectHash,
            IssuedAt,
            IssuedAt));
    }

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

    private static TailorInvitation CreateInvitation() => TailorInvitation.Create(
        "ana@example.com",
        new TailorProfileId(Guid.CreateVersion7()),
        CorrectHash,
        IssuedAt,
        IssuedAt.AddDays(1));
}
