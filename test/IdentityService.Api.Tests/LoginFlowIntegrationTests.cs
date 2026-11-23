using System.Net;
using System.Net.Http.Json;
using IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace IdentityService.Api.Tests;

[Trait("Category", "Integration")]
public sealed class LoginFlowIntegrationTests
{
    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build();
        await postgres.StartAsync();

        var originalValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings__IdentityDb"] = Environment.GetEnvironmentVariable("ConnectionStrings__IdentityDb"),
            ["Jwt__SigningKey"] = Environment.GetEnvironmentVariable("Jwt__SigningKey"),
            ["AdminSeed__Email"] = Environment.GetEnvironmentVariable("AdminSeed__Email"),
            ["AdminSeed__Password"] = Environment.GetEnvironmentVariable("AdminSeed__Password")
        };

        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__IdentityDb", postgres.GetConnectionString());
            Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-test-signing-key-with-at-least-32-bytes");
            Environment.SetEnvironmentVariable("AdminSeed__Email", "admin@example.test");
            Environment.SetEnvironmentVariable("AdminSeed__Password", "Admin1234!");

            var options = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .Options;
            await using (var db = new IdentityDbContext(options))
                await db.Database.MigrateAsync();

            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseEnvironment(Environments.Development));
            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "unknown@example.test",
                password = "WrongPassword123!"
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            foreach (var (key, value) in originalValues)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
