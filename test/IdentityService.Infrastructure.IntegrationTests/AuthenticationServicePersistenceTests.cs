using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
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

    [Fact]
    public async Task Authenticate_AfterFiveWrongPasswords_LocksOutTheAccount()
    {
        var tailorProfileId = new TailorProfileId(Guid.CreateVersion7());
        var email = EmailAddress.Of("lockout.tailor@example.com");
        const string password = "StrongPassword123!";

        await using var setupRequest = postgres.NewRequest();
        var provisioner = setupRequest.ServiceProvider.GetRequiredService<IAccountProvisioner>();
        var provisionResult = await provisioner.CreateTailorAccountAsync(email, password, tailorProfileId);
        Assert.True(provisionResult.Succeeded);

        await using var authRequest = postgres.NewRequest();
        var authService = authRequest.ServiceProvider.GetRequiredService<IAuthenticationService>();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await authService.AuthenticateAsync(email, "WrongPassword123!");
            Assert.False(failed.Succeeded);
        }

        var validPasswordDuringLockout = await authService.AuthenticateAsync(email, password);

        Assert.False(validPasswordDuringLockout.Succeeded);
        Assert.Null(validPasswordDuringLockout.Token);
    }

    [Fact]
    public async Task Authenticate_WithUnknownEmail_PerformsPasswordHashVerification()
    {
        await using var authRequest = postgres.NewRequest();
        var userManager = authRequest.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var countingHasher = new CountingPasswordHasher(userManager.PasswordHasher);
        userManager.PasswordHasher = countingHasher;

        var authService = authRequest.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var result = await authService.AuthenticateAsync(
            EmailAddress.Of("missing.login@example.com"),
            "SomePassword123");

        Assert.False(result.Succeeded);
        Assert.Equal(1, countingHasher.VerificationCount);
    }

    [Fact]
    public async Task Authenticate_WithValidPassword_ResetsFailedAttemptCount()
    {
        var email = EmailAddress.Of("reset-attempts.tailor@example.com");
        const string password = "StrongPassword123!";

        await using (var setupRequest = postgres.NewRequest())
        {
            var provisioner = setupRequest.ServiceProvider.GetRequiredService<IAccountProvisioner>();
            var result = await provisioner.CreateTailorAccountAsync(
                email,
                password,
                new TailorProfileId(Guid.CreateVersion7()));
            Assert.True(result.Succeeded);
        }

        await using (var failedLoginRequest = postgres.NewRequest())
        {
            var authService = failedLoginRequest.ServiceProvider.GetRequiredService<IAuthenticationService>();
            await authService.AuthenticateAsync(email, "WrongPassword123!");
            await authService.AuthenticateAsync(email, "WrongPassword123!");
        }

        await using (var validLoginRequest = postgres.NewRequest())
        {
            var authService = validLoginRequest.ServiceProvider.GetRequiredService<IAuthenticationService>();
            var result = await authService.AuthenticateAsync(email, password);
            Assert.True(result.Succeeded);
        }

        await using var verificationRequest = postgres.NewRequest();
        var userManager = verificationRequest.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email.Value);
        Assert.NotNull(user);
        Assert.Equal(0, user.AccessFailedCount);
    }

    private sealed class CountingPasswordHasher(IPasswordHasher<AppUser> inner) : IPasswordHasher<AppUser>
    {
        public int VerificationCount { get; private set; }

        public string HashPassword(AppUser user, string password) => inner.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(
            AppUser user,
            string hashedPassword,
            string providedPassword)
        {
            VerificationCount++;
            return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }
}
