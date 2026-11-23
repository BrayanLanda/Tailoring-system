using IdentityService.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure.IntegrationTests;

public sealed class InvitationTokenServicesTests
{
    [Fact]
    public void GeneratedTokens_AreUniqueAndTheirHashesMatchTheRawValue()
    {
        using var serviceProvider = new ServiceCollection()
            .AddIdentityInfrastructure("Host=unused;Database=unused;Username=unused;Password=unused", TestConfiguration.WithJwtKey())
            .BuildServiceProvider();

        var generator = serviceProvider.GetRequiredService<IInvitationTokenGenerator>();
        var hasher = serviceProvider.GetRequiredService<IInvitationTokenHasher>();

        var first = generator.Generate();
        var second = generator.Generate();

        Assert.NotEqual(first.RawValue, second.RawValue);
        Assert.NotEqual(first.RawValue, first.Hash.Value);
        Assert.True(first.Hash.Matches(hasher.Hash(first.RawValue)));
        Assert.True(second.Hash.Matches(hasher.Hash(second.RawValue)));
    }
}
