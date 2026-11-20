namespace IdentityService.Infrastructure.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IdentityPostgresCollection : ICollectionFixture<IdentityPostgresFixture>
{
    public const string Name = "identity-postgres";
}
