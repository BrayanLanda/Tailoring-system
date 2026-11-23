using Microsoft.Extensions.Configuration;

namespace IdentityService.Infrastructure.IntegrationTests;

public class TestConfiguration
{
    // Test-only key: it never leaves the test process.
    public static IConfiguration WithJwtKey() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "test-only-signing-key-0123456789-abcdefghij"
        })
        .Build();
}
