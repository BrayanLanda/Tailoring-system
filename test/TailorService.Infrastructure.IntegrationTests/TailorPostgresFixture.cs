using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TailorService.Infrastructure;
using TailorService.Infrastructure.Persistence;

namespace TailorService.Infrastructure.IntegrationTests;

public sealed class TailorPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _serviceProvider = new ServiceCollection()
            .AddTailorInfrastructure(_container.GetConnectionString())
            .BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TailorDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _container.DisposeAsync();
    }

    public AsyncServiceScope NewRequest() => _serviceProvider.CreateAsyncScope();
}
