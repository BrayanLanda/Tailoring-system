using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TailorService.Infrastructure.Persistence;

internal sealed class TailorDbContextFactory : IDesignTimeDbContextFactory<TailorDbContext>
{
    public TailorDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__TailorDb")
            ?? throw new InvalidOperationException(
                "Set the ConnectionStrings__TailorDb environment variable before running EF Core tools.");

        var options = new DbContextOptionsBuilder<TailorDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TailorDbContext(options);
    }
}
