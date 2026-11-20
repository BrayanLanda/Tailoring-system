using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure.IntegrationTests;

[Collection(IdentityPostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AccountProvisioningPersistenceTests(IdentityPostgresFixture postgres)
{
    [Fact]
    public async Task CreateTailorAccount_PersistsUserWithTailorRoleAndTailorProfileId()
    {
        var tailorProfileId = new TailorProfileId(Guid.CreateVersion7());
        var email = EmailAddress.Of("tailor.maria@example.com");
        const string password = "StrongPassword123!";

        await using var writeRequest = postgres.NewRequest();
        var provisioner = writeRequest.ServiceProvider.GetRequiredService<IAccountProvisioner>();

        var result = await provisioner.CreateTailorAccountAsync(email, password, tailorProfileId);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);

        // Verify in another independent scope / DbContext
        await using var readRequest = postgres.NewRequest();
        var userManager = readRequest.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = await userManager.FindByEmailAsync(email.Value);
        Assert.NotNull(user);
        Assert.Equal(email.Value, user.Email);
        Assert.Equal(tailorProfileId.Value, user.TailorProfileId);

        var isTailor = await userManager.IsInRoleAsync(user, AppRole.Tailor);
        Assert.True(isTailor);

        var passwordValid = await userManager.CheckPasswordAsync(user, password);
        Assert.True(passwordValid);
    }
}
