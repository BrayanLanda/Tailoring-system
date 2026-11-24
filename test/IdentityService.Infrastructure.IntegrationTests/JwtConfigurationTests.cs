using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure.IntegrationTests;

public sealed class JwtConfigurationTests
{
    private const string ConnectionString = "Host=unused;Database=unused;Username=unused;Password=unused";

    [Fact]
    public void WithoutAPrivateKey_RegistrationFails()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddIdentityInfrastructure(ConnectionString, configuration));
    }

    [Fact]
    public void WithInvalidPrivateKey_RegistrationFails()
    {
        var configuration = Configuration("not a PEM key");

        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddIdentityInfrastructure(ConnectionString, configuration));
    }

    [Fact]
    public void WithRsaPrivateKey_RegistrationSucceeds()
    {
        using var rsa = RSA.Create(2048);
        var configuration = Configuration(rsa.ExportPkcs8PrivateKeyPem());

        var services = new ServiceCollection().AddIdentityInfrastructure(ConnectionString, configuration);

        Assert.NotNull(services);
    }

    private static IConfiguration Configuration(string privateKeyPem) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:PrivateKeyPem"] = privateKeyPem })
            .Build();
}
