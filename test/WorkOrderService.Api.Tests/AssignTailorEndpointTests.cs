using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using WorkOrderService.Api.WorkOrders;
using WorkOrderService.Application.WorkOrders.CreateWorkOrder;
using WorkOrderService.Domain.ValueObjects;
using WorkOrderService.Infrastructure.Persistence;
using WorkOrderService.Infrastructure.Persistence.Entities;

namespace WorkOrderService.Api.Tests;

[Trait("Category", "Integration")]
public sealed class AssignTailorEndpointTests : IAsyncLifetime
{
    private const string Issuer = "sastreria-identity";
    private const string Audience = "sastreria-api";
    private const string AdminRole = "Admin";
    private static readonly RSA SigningRsa = RSA.Create(2048);
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly Dictionary<string, string?> _originalEnvironment = [];
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _workOrderId;
    private Guid _tailorId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var publicKeyPem = SigningRsa.ExportSubjectPublicKeyInfoPem();
        SetEnvironment("ConnectionStrings__WorkOrderDb", _postgres.GetConnectionString());
        SetEnvironment("RABBITMQ_USER", "api-test");
        SetEnvironment("RABBITMQ_PASSWORD", "api-test-password");
        SetEnvironment("Jwt__PublicKeyPem", publicKeyPem);
        SetEnvironment("Jwt__Issuer", Issuer);
        SetEnvironment("Jwt__Audience", Audience);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });
        _client = _factory.CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();
        await dbContext.Database.MigrateAsync();

        var createResult = await scope.ServiceProvider.GetRequiredService<CreateWorkOrderHandler>()
            .HandleAsync(new CreateWorkOrderCommand(
                "API Test Customer",
                "3001234567",
                "Trouser",
                [new CreateWorkOrderJob("Hem", 1)]));
        _workOrderId = createResult.Id.Value;
        _tailorId = Guid.NewGuid();
        dbContext.AssignableTailors.Add(AssignableTailorProjection.Create(
            _tailorId,
            true,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
        foreach (var (key, value) in _originalEnvironment)
            Environment.SetEnvironmentVariable(key, value);
    }

    [Fact]
    public async Task AssignTailor_RequiresAdminAndPersistsAssignment()
    {
        var route = $"/api/work-orders/{_workOrderId}/assignments";

        using var anonymousResponse = await _client.PostAsJsonAsync(route, new { tailorId = _tailorId });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var tailorRequest = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(new { tailorId = _tailorId })
        };
        tailorRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("Tailor"));
        using var tailorResponse = await _client.SendAsync(tailorRequest);
        Assert.Equal(HttpStatusCode.Forbidden, tailorResponse.StatusCode);

        using var symmetricRequest = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(new { tailorId = _tailorId })
        };
        symmetricRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateHmacToken(AdminRole));
        using var symmetricResponse = await _client.SendAsync(symmetricRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, symmetricResponse.StatusCode);

        using var adminRequest = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(new { tailorId = _tailorId })
        };
        adminRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(AdminRole));
        using var adminResponse = await _client.SendAsync(adminRequest);

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        var result = await adminResponse.Content.ReadFromJsonAsync<AssignTailorResponse>();
        Assert.NotNull(result);
        Assert.Equal(_workOrderId, result.WorkOrderId);
        Assert.Equal(_tailorId, result.TailorId);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var repository = verificationScope.ServiceProvider
            .GetRequiredService<WorkOrderService.Domain.Repositories.IWorkOrderRepository>();
        var workOrder = await repository.GetByIdAsync(new WorkOrderId(_workOrderId));
        Assert.NotNull(workOrder);
        Assert.Equal(new TailorId(_tailorId), workOrder.TailorId);
    }

    private static string CreateToken(string role)
    {
        var credentials = new SigningCredentials(
            new RsaSecurityKey(SigningRsa),
            SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", role)],
            DateTime.UtcNow.AddSeconds(-1),
            DateTime.UtcNow.AddMinutes(5),
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateHmacToken(string role)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("not-the-rsa-key-but-a-long-test-secret")),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", role)],
            DateTime.UtcNow.AddSeconds(-1),
            DateTime.UtcNow.AddMinutes(5),
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private void SetEnvironment(string key, string value)
    {
        _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }
}
