using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IdentityService.Api.Auth;
using IdentityService.Api.TailorInvitations;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace IdentityService.Api.Tests;

[Trait("Category", "Integration")]
public sealed class LoginFlowIntegrationTests : IAsyncLifetime
{
    private const string AdminEmail = "admin@example.test";
    private const string AdminPassword = "Admin1234!";
    private const string SigningKey = "integration-test-signing-key-with-at-least-32-bytes";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly CapturingInvitationDelivery _delivery = new();
    private readonly Dictionary<string, string?> _originalEnvironment = [];
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        SetEnvironment("ConnectionStrings__IdentityDb", _postgres.GetConnectionString());
        SetEnvironment("Jwt__SigningKey", SigningKey);
        SetEnvironment("AdminSeed__Email", AdminEmail);
        SetEnvironment("AdminSeed__Password", AdminPassword);

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        await using (var db = new IdentityDbContext(options))
            await db.Database.MigrateAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInvitationDelivery>();
                services.AddSingleton<IInvitationDelivery>(_delivery);
            });
        });
        _client = _factory.CreateClient();
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
    public async Task Admin_InvitesTailor_TailorAcceptsAndLogsInWithRoleAndProfileClaims()
    {
        using var adminLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AdminEmail,
            password = AdminPassword
        });
        Assert.Equal(HttpStatusCode.OK, adminLoginResponse.StatusCode);
        var adminLogin = await adminLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(adminLogin);

        var tailorProfileId = Guid.NewGuid();
        using var issueRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tailor-invitations/")
        {
            Content = JsonContent.Create(new { email = "tailor@example.test", tailorProfileId })
        };
        issueRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminLogin.Token);

        using var issueResponse = await _client.SendAsync(issueRequest);
        Assert.Equal(HttpStatusCode.Created, issueResponse.StatusCode);
        var invitation = await issueResponse.Content.ReadFromJsonAsync<IssueTailorInvitationResponse>();
        Assert.NotNull(invitation);
        Assert.Equal(invitation.InvitationId, _delivery.InvitationId);
        Assert.False(string.IsNullOrWhiteSpace(_delivery.Token));

        using var acceptResponse = await _client.PostAsJsonAsync("/api/tailor-invitations/accept", new
        {
            invitationId = invitation.InvitationId,
            token = _delivery.Token,
            password = "TailorPass123"
        });
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        using var tailorLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "tailor@example.test",
            password = "TailorPass123"
        });
        Assert.Equal(HttpStatusCode.OK, tailorLoginResponse.StatusCode);
        var tailorLogin = await tailorLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(tailorLogin);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(tailorLogin.Token);
        Assert.Contains(token.Claims, claim => claim.Type == "role" && claim.Value == AppRole.Tailor);
        Assert.Contains(token.Claims, claim => claim.Type == "tailor_profile_id" && claim.Value == tailorProfileId.ToString());
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "unknown@example.test",
            password = "WrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private void SetEnvironment(string key, string value)
    {
        _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    private sealed class CapturingInvitationDelivery : IInvitationDelivery
    {
        public Guid InvitationId { get; private set; }
        public string? Token { get; private set; }

        public Task DeliverAsync(
            EmailAddress email,
            TailorInvitationId invitationId,
            string rawToken,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default)
        {
            InvitationId = invitationId.Value;
            Token = rawToken;
            return Task.CompletedTask;
        }
    }
}
