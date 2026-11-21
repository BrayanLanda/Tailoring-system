using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TailorService.Application.Abstractions;
using TailorService.Domain.Repositories;
using TailorService.Infrastructure.Messaging;
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

    public static IServiceCollection AddTailorMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var username = configuration["RABBITMQ_USER"]
            ?? throw new InvalidOperationException("Set RABBITMQ_USER before starting TailorService.Api.");
        var password = configuration["RABBITMQ_PASSWORD"]
            ?? throw new InvalidOperationException("Set RABBITMQ_PASSWORD before starting TailorService.Api.");
        var options = new RabbitMqOptions(
            configuration["RABBITMQ_HOST"] ?? "localhost",
            int.TryParse(configuration["RABBITMQ_PORT"], out var port) ? port : 5672,
            username,
            password);

        services.AddSingleton(options);
        services.AddSingleton<RabbitMqIntegrationEventPublisher>();
        services.AddSingleton<IIntegrationEventPublisher>(provider =>
            provider.GetRequiredService<RabbitMqIntegrationEventPublisher>());
        services.AddScoped<OutboxDispatcher>();
        return services;
    }
}
