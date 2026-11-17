using TheTesting.Shared;

namespace CqrsApiExample.Infrastructure.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}
