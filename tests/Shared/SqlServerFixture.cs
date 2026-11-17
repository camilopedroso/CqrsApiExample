using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace CqrsApiExample.Tests.Shared;

/// <summary>
///     One SQL Server container per test assembly (shared through an xUnit
///     collection). Each caller gets its own database, so tests don't share
///     data. Requires Docker.
/// </summary>
/// <remarks>Linked into the integration test projects; see tests/Shared.</remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Same image as compose.yaml. SQL Server has no ARM image; on Apple Silicon it runs emulated.
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Connection string for a new database; the migrations create it on first use.</summary>
    public string CreateDatabaseConnectionString()
    {
        return new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"tests_{Guid.NewGuid():N}"
        }.ConnectionString;
    }
}
