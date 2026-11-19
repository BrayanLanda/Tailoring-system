using IdentityService.Domain.Entities;
using IdentityService.Domain.Repositories;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Tests.Fakes;

internal sealed class InMemoryTailorInvitationRepository : ITailorInvitationRepository
{
    public List<TailorInvitation> Invitations { get; } = [];

    public Task AddAsync(TailorInvitation invitation, CancellationToken cancellationToken = default)
    {
        Invitations.Add(invitation);
        return Task.CompletedTask;
    }

    public Task<TailorInvitation?> GetByTailorProfileIdAsync(
        TailorProfileId tailorProfileId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Invitations.FirstOrDefault(invitation => invitation.TailorProfileId == tailorProfileId));
}
