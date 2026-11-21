using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Security;

public sealed class IdentityDataSeeder(
    RoleManager<AppRole> roleManager,
    UserManager<AppUser> userManager,
    IOptions<AdminSeedOptions> adminOptions,
    ILogger<IdentityDataSeeder> logger)
{
    private static readonly string[] Roles = [AppRole.Admin, AppRole.Tailor, AppRole.Receptionist];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // 1. Ensure all standard roles exist
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new AppRole(role));
                logger.LogInformation("Role '{Role}' seeded successfully.", role);
            }
        }

        // 2. Seed initial admin account if configured
        var adminConfig = adminOptions.Value;
        if (string.IsNullOrWhiteSpace(adminConfig.Email) || string.IsNullOrWhiteSpace(adminConfig.Password))
        {
            return;
        }

        var existingAdmin = await userManager.FindByEmailAsync(adminConfig.Email);
        if (existingAdmin is null)
        {
            var adminUser = new AppUser
            {
                Id = Guid.CreateVersion7(),
                UserName = adminConfig.Email,
                Email = adminConfig.Email,
                EmailConfirmed = true
            };

            var creationResult = await userManager.CreateAsync(adminUser, adminConfig.Password);
            if (creationResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, AppRole.Admin);
                logger.LogInformation("Initial Admin account seeded successfully for '{Email}'.", adminConfig.Email);
            }
            else
            {
                logger.LogError("Failed to seed initial admin: {Errors}",
                    string.Join("; ", creationResult.Errors.Select(e => e.Description)));
            }
        }
    }
}
