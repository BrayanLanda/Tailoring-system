using IdentityService.Application.Abstractions;
using IdentityService.Application.Exceptions;
using IdentityService.Application.TailorInvitations.AcceptTailorInvitation;
using IdentityService.Application.Tests.Fakes;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Tests.TailorInvitations;

public sealed class AcceptTailorInvitationHandlerTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(2);
    private const string RawToken = "the-raw-token";
    private const string Password = "Str0ng-password!";

    private readonly InMemoryTailorInvitationRepository _repository = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly FakeInvitationTokenHasher _hasher = new();
    private readonly FakeAccountProvisioner _accounts = new();

    [Fact]
    public async Task HandleAsync_ValidToken_AcceptsTheInvitationAndCreatesTheAccount()
    {
        var invitation = PendingInvitation();
        var now = IssuedAt.AddHours(1);

        var result = await CreateHandler(now).HandleAsync(
            new AcceptTailorInvitationCommand(invitation.Id.Value, RawToken, Password));

        Assert.Equal(now, invitation.AcceptedAt);
        var account = Assert.Single(_accounts.Accounts);
        Assert.Equal(invitation.Email, account.Email);
        Assert.Equal(invitation.TailorProfileId, account.TailorProfileId);
        Assert.Equal(Password, account.Password);
        Assert.Equal(1, _unitOfWork.SaveChangesCalls);
        Assert.Equal(invitation.Email, result.Email);
    }

    [Fact]
    public async Task HandleAsync_WrongToken_FailsAndCreatesNothing()
    {
        var invitation = PendingInvitation();

        await Assert.ThrowsAsync<DomainException>(() => CreateHandler(IssuedAt.AddHours(1)).HandleAsync(
            new AcceptTailorInvitationCommand(invitation.Id.Value, "another-token", Password)));

        Assert.Null(invitation.AcceptedAt);
        Assert.Empty(_accounts.Accounts);
        Assert.Equal(0, _unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_UnknownInvitation_GivesTheSameAnswerAsAWrongToken()
    {
        var invitation = PendingInvitation();
        var handler = CreateHandler(IssuedAt.AddHours(1));

        var wrongToken = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new AcceptTailorInvitationCommand(invitation.Id.Value, "another-token", Password)));
        var unknownInvitation = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new AcceptTailorInvitationCommand(Guid.CreateVersion7(), RawToken, Password)));

        // The caller must not be able to tell whether an invitation exists.
        Assert.Equal(wrongToken.Message, unknownInvitation.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_EmptyToken_FailsWithTheGenericError(string token)
    {
        var invitation = PendingInvitation();

        var exception = await Assert.ThrowsAsync<DomainException>(() => CreateHandler(IssuedAt.AddHours(1)).HandleAsync(
            new AcceptTailorInvitationCommand(invitation.Id.Value, token, Password)));

        Assert.Equal(TailorInvitation.NotValidMessage, exception.Message);
        Assert.Empty(_accounts.Accounts);
    }

    [Fact]
    public async Task HandleAsync_ExpiredInvitationWithTheCorrectToken_FailsAndCreatesNothing()
    {
        var invitation = PendingInvitation();

        await Assert.ThrowsAsync<DomainException>(() => CreateHandler(invitation.ExpiresAt).HandleAsync(
            new AcceptTailorInvitationCommand(invitation.Id.Value, RawToken, Password)));

        Assert.Empty(_accounts.Accounts);
        Assert.Equal(0, _unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_AlreadyAcceptedInvitation_DoesNotCreateASecondAccount()
    {
        var invitation = PendingInvitation();
        var handler = CreateHandler(IssuedAt.AddHours(1));
        var command = new AcceptTailorInvitationCommand(invitation.Id.Value, RawToken, Password);
        await handler.HandleAsync(command);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));

        Assert.Single(_accounts.Accounts);
    }

    [Fact]
    public async Task HandleAsync_WhenTheAccountCannotBeCreated_DoesNotSaveAndReportsTheReasons()
    {
        var invitation = PendingInvitation();
        _accounts.Result = AccountCreationResult.Failure("Password must contain a digit.");

        var exception = await Assert.ThrowsAsync<AccountCreationFailedException>(() =>
            CreateHandler(IssuedAt.AddHours(1)).HandleAsync(
                new AcceptTailorInvitationCommand(invitation.Id.Value, RawToken, "weak")));

        Assert.Equal("Password must contain a digit.", Assert.Single(exception.Errors));
        Assert.Equal(0, _unitOfWork.SaveChangesCalls);
    }

    private AcceptTailorInvitationHandler CreateHandler(DateTimeOffset now) =>
        new(_repository, _unitOfWork, _hasher, _accounts, new FixedTimeProvider(now));

    private TailorInvitation PendingInvitation()
    {
        var invitation = TailorInvitation.Create(
            EmailAddress.Of("ana@example.com"),
            new TailorProfileId(Guid.CreateVersion7()),
            _hasher.Hash(RawToken),
            IssuedAt,
            IssuedAt.Add(Lifetime));

        _repository.Invitations.Add(invitation);
        return invitation;
    }
}
