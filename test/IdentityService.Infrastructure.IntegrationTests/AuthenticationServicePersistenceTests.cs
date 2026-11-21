using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure.IntegrationTests;

[Collection(IdentityPostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AuthenticationServicePersistenceTests(IdentityPostgresFixture postgres)
{
    [Fact]
    public async Task Authenticate_WithValidCredentials_ReturnsJwtWithRolesAndTailorProfileId()
    {
        var tailorProfileId = new TailorProfileId(Guid.CreateVersion7());
        var email = EmailAddress.Of("login.tailor@example.com");
        const string password = "StrongPassword123!";

        // 1. Provision account
        await using var setupRequest = postgres.NewRequest();
        var provisioner = setupRequest.ServiceProvider.GetRequiredService<IAccountProvisioner>();
        var provisionResult = await provisioner.CreateTailorAccountAsync(email, password, tailorProfileId);
        Assert.True(provisionResult.Succeeded);

        // 2. Authenticate
        await using var authRequest = postgres.NewRequest();
        var authService = authRequest.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var authResult = await authService.AuthenticateAsync(email, password);

        Assert.True(authResult.Succeeded);
        Assert.NotNull(authResult.Token);
        Assert.NotNull(authResult.ExpiresAt);

        // 3. Validate Token Claims
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.ReadJwtToken(authResult.Token);

        Assert.Equal(email.Value, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Contains(jwt.Claims, c => (c.Type == "role" || c.Type == ClaimTypes.Role) && c.Value == "Tailor");
        Assert.Equal(tailorProfileId.Value.ToString(), jwt.Claims.First(c => c.Type == "tailor_profile_id").Value);
    }

    [Fact]
    public async Task Authenticate_WithWrongPassword_Fails()
    {
        var tailorProfileId = new TailorProfileId(Guid.CreateVersion7());
        var email = EmailAddress.Of("wrongpass.tailor@example.com");
        const string password = "StrongPassword123!";

        await using var setupRequest = postgres.NewRequest();
        var provisioner = setupRequest.ServiceProvider.GetRequiredService<IAccountProvisioner>();
        await provisioner.CreateTailorAccountAsync(email, password, tailorProfileId);

        await using var authRequest = postgres.NewRequest();
        var authService = authRequest.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var authResult = await authService.AuthenticateAsync(email, "IncorrectPassword!");

        Assert.False(authResult.Succeeded);
        Assert.Null(authResult.Token);
    }
}
