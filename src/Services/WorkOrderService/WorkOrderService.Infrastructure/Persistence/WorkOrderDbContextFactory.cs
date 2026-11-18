using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkOrderService.Infrastructure.Persistence;

internal sealed class WorkOrderDbContextFactory : IDesignTimeDbContextFactory<WorkOrderDbContext>
{
    public WorkOrderDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__WorkOrderDb")
            ?? throw new InvalidOperationException(
                "Set the ConnectionStrings__WorkOrderDb environment variable before running EF Core tools.");

        var options = new DbContextOptionsBuilder<WorkOrderDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new WorkOrderDbContext(options);
    }
}
