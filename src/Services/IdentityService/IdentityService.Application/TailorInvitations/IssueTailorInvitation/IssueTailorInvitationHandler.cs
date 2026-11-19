using IdentityService.Application.Abstractions;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.Repositories;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.TailorInvitations.IssueTailorInvitation;

public sealed class IssueTailorInvitationHandler
{
    private readonly ITailorInvitationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInvitationTokenGenerator _tokenGenerator;
    private readonly IInvitationDelivery _delivery;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _invitationLifetime;

    public IssueTailorInvitationHandler(
        ITailorInvitationRepository repository,
        IUnitOfWork unitOfWork,
        IInvitationTokenGenerator tokenGenerator,
        IInvitationDelivery delivery,
        TimeProvider timeProvider,
        TimeSpan invitationLifetime)
    {
        if (invitationLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(invitationLifetime), "Invitation lifetime must be positive.");

        _repository = repository;
        _unitOfWork = unitOfWork;
        _tokenGenerator = tokenGenerator;
        _delivery = delivery;
        _timeProvider = timeProvider;
        _invitationLifetime = invitationLifetime;
    }

    public async Task<IssueTailorInvitationResult> HandleAsync(
        IssueTailorInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = EmailAddress.Of(command.Email);
        var tailorProfileId = new TailorProfileId(command.TailorProfileId);
        if (tailorProfileId.Value == Guid.Empty)
            throw new DomainException("A tailor profile ID is required for the invitation.");

        var issuedAt = _timeProvider.GetUtcNow();
        var expiresAt = issuedAt.Add(_invitationLifetime);
        var token = _tokenGenerator.Generate();

        // One invitation per tailor profile: inviting again replaces the previous token.
        var invitation = await _repository.GetByTailorProfileIdAsync(tailorProfileId, cancellationToken);
        if (invitation is null)
        {
            invitation = TailorInvitation.Create(
                email.Value,
                tailorProfileId,
                token.Hash,
                issuedAt,
                expiresAt);

            await _repository.AddAsync(invitation, cancellationToken);
        }
        else
        {
            invitation.Reissue(email, token.Hash, issuedAt, expiresAt);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _delivery.DeliverAsync(email, invitation.Id, token.RawValue, expiresAt, cancellationToken);

        return new IssueTailorInvitationResult(invitation.Id, expiresAt);
    }
}
