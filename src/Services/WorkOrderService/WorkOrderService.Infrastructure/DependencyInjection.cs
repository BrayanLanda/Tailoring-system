using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Domain.Repositories;
using WorkOrderService.Domain.Services;
using WorkOrderService.Infrastructure.Persistence;
using WorkOrderService.Infrastructure.Persistence.Repositories;
using WorkOrderService.Infrastructure.Messaging;

namespace WorkOrderService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkOrderAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var publicKeyPath = configuration["Jwt:PublicKeyPath"];
        var publicKeyPem = configuration["Jwt:PublicKeyPem"]
            ?? (string.IsNullOrWhiteSpace(publicKeyPath) ? null : File.ReadAllText(publicKeyPath));
        if (string.IsNullOrWhiteSpace(publicKeyPem))
            throw new InvalidOperationException("Configure Jwt:PublicKeyPath (or Jwt:PublicKeyPem) for WorkOrderService token validation.");
        if (publicKeyPem.Contains("PRIVATE KEY", StringComparison.Ordinal))
            throw new InvalidOperationException("WorkOrderService must receive only the JWT public key, never the private key.");

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(publicKeyPem);
            if (rsa.KeySize < 2048)
            {
                rsa.Dispose();
                throw new InvalidOperationException("Jwt:PublicKeyPem must contain an RSA public key of at least 2048 bits.");
            }
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            rsa.Dispose();
            throw new InvalidOperationException("Jwt:PublicKeyPem must contain a valid RSA public key of at least 2048 bits.", exception);
        }

        var keyId = Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))[..16];
        var signingKey = new RsaSecurityKey(rsa) { KeyId = keyId };
        services.AddSingleton<SecurityKey>(signingKey);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "sastreria-identity",
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"] ?? "sastreria-api",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    RoleClaimType = "role",
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
                };
            });
        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddWorkOrderInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<WorkOrderDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddScoped<IWorkOrderRepository, EfWorkOrderRepository>();
        services.AddScoped<IAssignableTailorRepository, EfAssignableTailorRepository>();
        services.AddScoped<TailorProjectionUpdater>();
        services.AddScoped<ITailorProjectionUpdater>(provider =>
            provider.GetRequiredService<TailorProjectionUpdater>());
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ISerialNumberGenerator, PostgresSerialNumberGenerator>();

        return services;
    }

    public static IServiceCollection AddWorkOrderMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(RabbitMqOptions.FromConfiguration(configuration));
        services.AddSingleton<TailorEventConsumer>();
        return services;
    }
}
