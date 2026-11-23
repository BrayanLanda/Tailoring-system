using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WorkOrderService.Infrastructure.Persistence;

namespace WorkOrderService.Infrastructure.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();
    private ServiceProvider _service = null!;
    public string ConnectionString { get; private set; } = string.Empty;
    public IServiceScopeFactory ScopeFactory => _service.GetRequiredService<IServiceScopeFactory>();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        _service = new ServiceCollection()
            .AddWorkOrderInfrastructure(ConnectionString)
            .BuildServiceProvider();

        await using var scope = _service.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _service.DisposeAsync();
        await _container.DisposeAsync();
    }

    public AsyncServiceScope NewRequest() => _service.CreateAsyncScope();
}
