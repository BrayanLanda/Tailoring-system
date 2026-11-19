namespace TailorService.Infrastructure.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class TailorPostgresCollection : ICollectionFixture<TailorPostgresFixture>
{
    public const string Name = "tailor-postgres";
}
