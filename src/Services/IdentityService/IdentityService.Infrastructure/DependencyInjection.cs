using IdentityService.Application.Abstractions;
using IdentityService.Domain.Repositories;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Repositories;
using IdentityService.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddScoped<ITailorInvitationRepository, EfTailorInvitationRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IInvitationTokenHasher, Sha256InvitationTokenHasher>();
        services.AddSingleton<IInvitationTokenGenerator, SecureInvitationTokenGenerator>();

        return services;
    }
}
