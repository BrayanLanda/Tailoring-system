using IdentityService.Domain.Entities;
using IdentityService.Domain.Repositories;

namespace IdentityService.Application.Tests.Fakes;

internal sealed class InMemoryTailorInvitationRepository : ITailorInvitationRepository
{
    public List<TailorInvitation> Invitations { get; } = [];

    public Task AddAsync(TailorInvitation invitation, CancellationToken cancellationToken = default)
    {
        Invitations.Add(invitation);
        return Task.CompletedTask;
    }
}
