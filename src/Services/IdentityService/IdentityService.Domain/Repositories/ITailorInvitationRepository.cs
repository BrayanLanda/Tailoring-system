using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Repositories;

public interface ITailorInvitationRepository
{
    Task AddAsync(TailorInvitation invitation, CancellationToken cancellationToken = default);
}
