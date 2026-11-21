using IdentityService.Infrastructure.Persistence.Entities;
using IdentityService.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure.IntegrationTests;

[Collection(IdentityPostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class IdentityDataSeederPersistenceTests(IdentityPostgresFixture postgres)
{
    [Fact]
    public async Task SeedAsync_EnsuresAllRolesArePresent()
    {
        await using var request = postgres.NewRequest();
        var seeder = request.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
        var roleManager = request.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        await seeder.SeedAsync();

        Assert.True(await roleManager.RoleExistsAsync(AppRole.Admin));
        Assert.True(await roleManager.RoleExistsAsync(AppRole.Tailor));
        Assert.True(await roleManager.RoleExistsAsync(AppRole.Receptionist));
    }
}
