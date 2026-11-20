using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;

namespace IdentityService.Infrastructure.Security;

public sealed class AspNetIdentityAccountProvisioner(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager) : IAccountProvisioner
{
    public async Task<AccountCreationResult> CreateTailorAccountAsync(
        EmailAddress email,
        string password,
        TailorProfileId tailorProfileId,
        CancellationToken cancellationToken = default)
    {
        // Ensure Tailor role exists
        if (!await roleManager.RoleExistsAsync(AppRole.Tailor))
        {
            await roleManager.CreateAsync(new AppRole(AppRole.Tailor));
        }

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email.Value,
            Email = email.Value,
            TailorProfileId = tailorProfileId.Value
        };

        var creationResult = await userManager.CreateAsync(user, password);
        if (!creationResult.Succeeded)
        {
            return AccountCreationResult.Failure(
                creationResult.Errors.Select(e => e.Description).ToArray());
        }

        var roleResult = await userManager.AddToRoleAsync(user, AppRole.Tailor);
        if (!roleResult.Succeeded)
        {
            return AccountCreationResult.Failure(
                roleResult.Errors.Select(e => e.Description).ToArray());
        }

        return AccountCreationResult.Success();
    }
}
