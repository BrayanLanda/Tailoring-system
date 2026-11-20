using IdentityService.Domain.Entities;
using IdentityService.Domain.Repositories;
using IdentityService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence.Repositories;

internal sealed class EfTailorInvitationRepository(IdentityDbContext dbContext) : ITailorInvitationRepository
{
    public async Task AddAsync(TailorInvitation invitation, CancellationToken cancellationToken = default) =>
        await dbContext.TailorInvitations.AddAsync(invitation, cancellationToken);

    public Task<TailorInvitation?> GetByIdAsync(
        TailorInvitationId id,
        CancellationToken cancellationToken = default) =>
        dbContext.TailorInvitations.SingleOrDefaultAsync(invitation => invitation.Id == id, cancellationToken);

    public Task<TailorInvitation?> GetByTailorProfileIdAsync(
        TailorProfileId tailorProfileId,
        CancellationToken cancellationToken = default) =>
        dbContext.TailorInvitations.SingleOrDefaultAsync(
            invitation => invitation.TailorProfileId == tailorProfileId,
            cancellationToken);
}
