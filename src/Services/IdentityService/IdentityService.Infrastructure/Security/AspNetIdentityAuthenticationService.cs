using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Infrastructure.Security;

public sealed class AspNetIdentityAuthenticationService(
    UserManager<AppUser> userManager,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : IAuthenticationService
{
    private static readonly AppUser TimingPaddingUser = new();
    private static readonly string TimingPaddingPasswordHash = CreateTimingPaddingPasswordHash();
    private readonly JwtOptions _options = jwtOptions.Value;

    public async Task<AuthenticationResult> AuthenticateAsync(
        EmailAddress email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email.Value);
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            VerifyDummyPassword(userManager, password);
            return AuthenticationResult.Failed("Invalid email or password.");
        }

        var isPasswordValid = await userManager.CheckPasswordAsync(user, password);
        if (!isPasswordValid)
        {
            await userManager.AccessFailedAsync(user);
            return AuthenticationResult.Failed("Invalid email or password.");
        }

        if (user.AccessFailedCount > 0)
            await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? email.Value),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (user.TailorProfileId.HasValue)
        {
            claims.Add(new Claim("tailor_profile_id", user.TailorProfileId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt.UtcDateTime,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var rawToken = tokenHandler.WriteToken(token);

        return AuthenticationResult.Success(rawToken, expiresAt);
    }

    private static void VerifyDummyPassword(UserManager<AppUser> userManager, string password) =>
        userManager.PasswordHasher.VerifyHashedPassword(TimingPaddingUser, TimingPaddingPasswordHash, password);

    private static string CreateTimingPaddingPasswordHash()
    {
        var randomPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new PasswordHasher<AppUser>().HashPassword(TimingPaddingUser, randomPassword);
    }
}
