using BenchmarkDotNet.Attributes;
using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Infrastructure.Persistence;
using CqrsApiExample.Infrastructure.Persistence.Ado;
using CqrsApiExample.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CqrsApiExample.Persistence.Benchmarks;

/// <summary>
///     What <c>CreateUserCommandHandler</c> does after validation: add a new user
///     and commit it with its <c>UserCreatedDomainEvent</c> outbox row, in one
///     transaction. Both sides pay the same <see cref="User.Create"/> cost. ADO.NET
///     runs in both <see cref="AdoWriteMode"/>s: one batched command (one round
///     trip) or one command per statement (BEGIN, two INSERTs, COMMIT).
/// </summary>
public class WriteBenchmarks
{
    private DbContextOptions<ApplicationDbContext> _efCoreOptions = null!;
    private SqlConnectionFactory _connections = null!;

    [GlobalSetup]
    public void Setup()
    {
        var connectionString = BenchmarkDatabase.Current.WritesConnectionString;
        _efCoreOptions = BenchmarkDatabase.EfCoreOptions(connectionString);
        _connections = new SqlConnectionFactory(connectionString);
    }

    [Benchmark(Baseline = true, Description = "EFCore")]
    [BenchmarkCategory("Add + SaveChanges (user + outbox)")]
    public async Task CreateUser_EfCore()
    {
        await using var context = new ApplicationDbContext(_efCoreOptions);
        new UserRepository(context).Add(NewUser());
        await new UnitOfWork(context, TimeProvider.System).SaveChangesAsync(CancellationToken.None);
    }

    [Benchmark(Description = "ADO.NET-batched")]
    [BenchmarkCategory("Add + SaveChanges (user + outbox)")]
    public Task CreateUser_Ado_Batched() => CreateUserWithAdoAsync(AdoWriteMode.Batched);

    [Benchmark(Description = "ADO.NET-per-command")]
    [BenchmarkCategory("Add + SaveChanges (user + outbox)")]
    public Task CreateUser_Ado_CommandPerStatement() => CreateUserWithAdoAsync(AdoWriteMode.CommandPerStatement);

    private async Task CreateUserWithAdoAsync(AdoWriteMode mode)
    {
        var changes = new AdoChangeSet();
        new AdoUserRepository(_connections, changes).Add(NewUser());
        await new AdoUnitOfWork(_connections, changes, TimeProvider.System, mode).SaveChangesAsync(CancellationToken.None);
    }

    private static User NewUser() =>
        User.Create("Ana", "Silva", $"{Guid.NewGuid():N}@benchmark.com", "hashed-password").Value;
}
