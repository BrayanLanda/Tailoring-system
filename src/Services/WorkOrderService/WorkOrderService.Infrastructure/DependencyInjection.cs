using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Domain.Repositories;
using WorkOrderService.Domain.Services;
using WorkOrderService.Infrastructure.Persistence;
using WorkOrderService.Infrastructure.Persistence.Repositories;
using WorkOrderService.Infrastructure.Messaging;

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
