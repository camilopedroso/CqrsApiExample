using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Infrastructure.Persistence;
using CqrsApiExample.Infrastructure.Persistence.Interceptors;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace CqrsApiExample.Persistence.Benchmarks;

/// <summary>
///     The SQL Server the benchmarks run against. By default a throwaway
///     container (Testcontainers, so Docker is required); set
///     <c>BENCHMARK_CONNECTION_STRING</c> to use an existing server instead.
///     Reads and writes get separate databases, so the inserts of the write
///     benchmarks never change what the read benchmarks return.
/// </summary>
internal sealed class BenchmarkDatabase : IAsyncDisposable
{
    public const int SeededUsers = 100;
    private const string ConnectionStringVariable = "BENCHMARK_CONNECTION_STRING";

    private readonly MsSqlContainer? _container;

    private BenchmarkDatabase(MsSqlContainer? container, string serverConnectionString)
    {
        _container = container;
        ReadsConnectionString = WithDatabase(serverConnectionString, "PersistenceBenchmarks_Reads");
        WritesConnectionString = WithDatabase(serverConnectionString, "PersistenceBenchmarks_Writes");
    }

    /// <summary>Set by <see cref="StartAsync"/>; the benchmarks run in-process and read it from here.</summary>
    public static BenchmarkDatabase Current { get; private set; } = null!;

    public string ReadsConnectionString { get; }

    public string WritesConnectionString { get; }

    public static async Task<BenchmarkDatabase> StartAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        MsSqlContainer? container = null;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine($"Starting a SQL Server container ({ConnectionStringVariable} not set)...");
            // Same image as compose.yaml and the integration tests.
            container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await container.StartAsync();
            connectionString = container.GetConnectionString();
        }

        var database = new BenchmarkDatabase(container, connectionString);
        await database.PrepareAsync(database.ReadsConnectionString, seed: true);
        await database.PrepareAsync(database.WritesConnectionString, seed: false);

        Current = database;
        return database;
    }

    /// <summary>Same EF Core setup as <c>AddPersistence</c>: retries and the audit interceptor.</summary>
    public static DbContextOptions<ApplicationDbContext> EfCoreOptions(string connectionString) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString,
                          sqlServer => sqlServer.EnableRetryOnFailure(maxRetryCount: 3,
                                                                      maxRetryDelay: TimeSpan.FromSeconds(5),
                                                                      errorNumbersToAdd: null))
            .AddInterceptors(new AuditableEntityInterceptor(TimeProvider.System))
            .Options;

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private async Task PrepareAsync(string connectionString, bool seed)
    {
        await using var context = new ApplicationDbContext(EfCoreOptions(connectionString));
        // A clean database every run, so results are comparable between runs.
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        if (!seed)
        {
            return;
        }

        for (var i = 0; i < SeededUsers; i++)
        {
            context.Users.Add(User.Create($"Name{i}", $"Surname{i}", $"user{i}@benchmark.com").Value);
        }

        await context.SaveChangesAsync();
    }

    private static string WithDatabase(string connectionString, string database) =>
        new SqlConnectionStringBuilder(connectionString) { InitialCatalog = database }.ConnectionString;
}
