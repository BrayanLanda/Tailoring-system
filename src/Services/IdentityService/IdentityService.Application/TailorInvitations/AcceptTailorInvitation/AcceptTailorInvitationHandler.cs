using IdentityService.Application.Abstractions;
using IdentityService.Application.Exceptions;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.Repositories;

namespace IdentityService.Application.TailorInvitations.AcceptTailorInvitation;

public sealed class AcceptTailorInvitationHandler(
    ITailorInvitationRepository repository,
    IUnitOfWork unitOfWork,
    IInvitationTokenHasher tokenHasher,
    IAccountProvisioner accounts,
    TimeProvider timeProvider)
{
    public async Task<AcceptTailorInvitationResult> HandleAsync(
    AcceptTailorInvitationCommand command,
    CancellationToken cancellationToken = default)
    {
        // Every way of being "not valid" answers with the same message, so nobody can tell
        // whether an invitation exists.
        if (string.IsNullOrWhiteSpace(command.Token))
            throw new DomainException(TailorInvitation.NotValidMessage);

        var presentedHash = tokenHasher.Hash(command.Token);

        var invitation = await repository.GetByIdAsync(new Domain.ValueObjects.TailorInvitationId(command.InvitationId), cancellationToken)
            ?? throw new DomainException(TailorInvitation.NotValidMessage);

        // The aggregate checks token, state and expiration. Only then is an account created:
        // creating it first would leave an account behind for an invitation that was never valid.
        invitation.Accept(presentedHash, timeProvider.GetUtcNow());

        var result = await accounts.CreateTailorAccountAsync(
            invitation.Email, command.Password, invitation.TailorProfileId, cancellationToken);

        if (!result.Succeeded)
            throw new AccountCreationFailedException(result.Errors);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AcceptTailorInvitationResult(invitation.Email);
    }
}
