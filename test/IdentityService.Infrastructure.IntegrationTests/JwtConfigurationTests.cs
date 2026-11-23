using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure.IntegrationTests;

public sealed class JwtConfigurationTests
{
    private const string ConnectionString = "Host=unused;Database=unused;Username=unused;Password=unused";

    [Fact]
    public void WithoutASigningKey_RegistrationFails()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddIdentityInfrastructure(ConnectionString, configuration));
    }

    [Fact]
    public void WithAShortSigningKey_RegistrationFails()
    {
        var configuration = Configuration("too-short");

        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddIdentityInfrastructure(ConnectionString, configuration));
    }

    [Fact]
    public void WithASufficientSigningKey_RegistrationSucceeds()
    {
        var configuration = Configuration(new string('k', 32));

        var services = new ServiceCollection().AddIdentityInfrastructure(ConnectionString, configuration);

        Assert.NotNull(services);
    }

    private static IConfiguration Configuration(string signingKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = signingKey })
            .Build();
}
