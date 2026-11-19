using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TailorService.Application.Abstractions;
using TailorService.Domain.Repositories;
using TailorService.Infrastructure.Persistence;
using TailorService.Infrastructure.Persistence.Repositories;

namespace TailorService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTailorInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<TailorDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddScoped<ITailorRepository, EfTailorRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }
}
