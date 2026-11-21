using System.Text;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Repositories;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Entities;
using IdentityService.Infrastructure.Persistence.Repositories;
using IdentityService.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
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
        IConfiguration? configuration = null)
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
        })
        .AddRoles<AppRole>()
        .AddEntityFrameworkStores<IdentityDbContext>();

        var jwtSection = configuration?.GetSection(JwtOptions.SectionName);
        if (jwtSection is not null && jwtSection.Exists())
        {
            services.Configure<JwtOptions>(jwtSection);
        }

        var jwtOptions = jwtSection?.Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            // Default development fallback key if not set
            jwtOptions.SigningKey = "sastreria-super-secret-development-signing-key-32chars!";
            services.Configure<JwtOptions>(options => options.SigningKey = jwtOptions.SigningKey);
        }

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

        var adminSection = configuration?.GetSection(AdminSeedOptions.SectionName);
        if (adminSection is not null && adminSection.Exists())
        {
            services.Configure<AdminSeedOptions>(adminSection);
        }
        services.AddScoped<IdentityDataSeeder>();

        return services;
    }
}
