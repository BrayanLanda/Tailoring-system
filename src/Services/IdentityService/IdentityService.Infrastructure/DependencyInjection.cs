using System.Text;
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
    private const int MinimumSigningKeyBytes = 32;

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

        // HS256 needs a key of at least 256 bits, and there is no default key: anyone who knew it
        // could forge a token for any user, including an administrator.
        if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < MinimumSigningKeyBytes)
            throw new InvalidOperationException(
                $"Jwt:SigningKey is missing or shorter than {MinimumSigningKeyBytes} bytes. " +
                "Set a random secret of at least 32 bytes (for example, 'openssl rand -base64 48').");

        services.Configure<JwtOptions>(jwtSection);

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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
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
