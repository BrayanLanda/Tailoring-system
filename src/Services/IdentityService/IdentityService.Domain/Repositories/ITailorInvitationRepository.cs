using IdentityService.Domain.Entities;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Domain.Repositories;

public interface ITailorInvitationRepository
{
    Task AddAsync(TailorInvitation invitation, CancellationToken cancellationToken = default);

    Task<TailorInvitation?> GetByTailorProfileIdAsync(
        TailorProfileId tailorProfileId,
        CancellationToken cancellationToken = default);
}
