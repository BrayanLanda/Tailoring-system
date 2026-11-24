using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace IdentityService.Infrastructure.IntegrationTests;

public class TestConfiguration
{
    // Test-only key: it never leaves the test process.
    private static readonly string PrivateKeyPem = CreatePrivateKeyPem();

    public static IConfiguration WithJwtKey() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:PrivateKeyPem"] = PrivateKeyPem
        })
        .Build();

    private static string CreatePrivateKeyPem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }
}
