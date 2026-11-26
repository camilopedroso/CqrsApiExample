using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Infrastructure.Persistence;
using CqrsApiExample.Infrastructure.Persistence.Ado;
using CqrsApiExample.Infrastructure.Persistence.Interceptors;
using CqrsApiExample.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace CqrsApiExample.Infrastructure.IntegrationTests;

/// <summary>A migrated database of its own, and a clock the test moves forward.</summary>
internal sealed class TestDatabase(SqlServerFixture sqlServer)
{
    public static readonly DateTimeOffset Start = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly string _connectionString = sqlServer.CreateDatabaseConnectionString();

    public TestClock Clock { get; } = new(Start);

    /// <summary>Who the audit fields record; anonymous until a test signs someone in.</summary>
    public TestCurrentUser CurrentUser { get; } = new();

    public async Task CreateAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DeleteAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString)
            .AddInterceptors(new AuditableEntityInterceptor(Clock, CurrentUser))
            .Options;

        return new ApplicationDbContext(options);
    }

    public UnitOfWork CreateUnitOfWork(ApplicationDbContext context) => new(context, Clock);

    /// <summary>The ADO.NET provider for one scope: repository and unit of work share a change set.</summary>
    public AdoScope CreateAdoScope(AdoWriteMode mode = AdoWriteMode.Batched)
    {
        var connections = new SqlConnectionFactory(_connectionString);
        var changes = new AdoChangeSet();
        return new AdoScope(new AdoUserRepository(connections, changes),
                            new AdoUnitOfWork(connections, changes, Clock, CurrentUser, mode),
                            new AdoUserQueries(connections));
    }
}

internal sealed record AdoScope(AdoUserRepository Repository, AdoUnitOfWork UnitOfWork, AdoUserQueries Queries);

/// <summary>An <see cref="ICurrentUser"/> a test can sign in and out.</summary>
internal sealed class TestCurrentUser : ICurrentUser
{
    public Guid? Id { get; set; }

    public bool IsAuthenticated => Id is not null;

    public bool IsInRole(string role) => false;
}

internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan duration) => Now += duration;
}
