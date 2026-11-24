using System.Security.Cryptography;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Repositories;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Entities;
using IdentityService.Infrastructure.Persistence.Repositories;
using IdentityService.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddIdentityCore<AppUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        })
        .AddRoles<AppRole>()
        .AddEntityFrameworkStores<IdentityDbContext>();

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        var privateKeyPem = string.IsNullOrWhiteSpace(jwtOptions.PrivateKeyPem)
            ? (string.IsNullOrWhiteSpace(jwtOptions.PrivateKeyPath) ? null : File.ReadAllText(jwtOptions.PrivateKeyPath))
            : jwtOptions.PrivateKeyPem;
        if (string.IsNullOrWhiteSpace(privateKeyPem))
            throw new InvalidOperationException("Configure Jwt:PrivateKeyPath (or Jwt:PrivateKeyPem); IdentityService signs tokens with its RSA private key.");

        JwtSigningMaterial signingMaterial;
        try
        {
            signingMaterial = new JwtSigningMaterial(privateKeyPem);
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException("Jwt:PrivateKeyPem must contain a valid RSA private key of at least 2048 bits.", exception);
        }

        services.Configure<JwtOptions>(jwtSection);
        services.AddSingleton(signingMaterial);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = signingMaterial.SecurityKey
                };
            });

        services.AddAuthorization();

        services.AddScoped<ITailorInvitationRepository, EfTailorInvitationRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IInvitationTokenHasher, Sha256InvitationTokenHasher>();
        services.AddSingleton<IInvitationTokenGenerator, SecureInvitationTokenGenerator>();
        services.AddScoped<IAccountProvisioner, AspNetIdentityAccountProvisioner>();
        services.AddScoped<IAuthenticationService, AspNetIdentityAuthenticationService>();

        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
        services.AddScoped<IdentityDataSeeder>();

        return services;
    }
}
