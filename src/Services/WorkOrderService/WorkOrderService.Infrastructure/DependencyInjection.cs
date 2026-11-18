using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Domain.Repositories;
using WorkOrderService.Domain.Services;
using WorkOrderService.Infrastructure.Persistence;
using WorkOrderService.Infrastructure.Persistence.Repositories;

namespace WorkOrderService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkOrderInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<WorkOrderDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddScoped<IWorkOrderRepository, EfWorkOrderRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ISerialNumberGenerator, PostgresSerialNumberGenerator>();

        return services;
    }
}
