using BenchmarkDotNet.Attributes;
using CqrsApiExample.Application.Common.Paging;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Infrastructure.Persistence;
using CqrsApiExample.Infrastructure.Persistence.Ado;
using CqrsApiExample.Infrastructure.Persistence.Queries;
using CqrsApiExample.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CqrsApiExample.Persistence.Benchmarks;

/// <summary>
///     The read side and the duplicate-email check, against a database seeded
///     with <see cref="BenchmarkDatabase.SeededUsers"/> users. Each EF Core
///     call gets a new <see cref="ApplicationDbContext"/>, as each HTTP request
///     does in the API.
/// </summary>
public class ReadBenchmarks
{
    // The largest page the API allows, so the list read still maps as many rows as it can.
    private const int PageSize = PagedResult<UserDto>.MaxPageSize;

    private DbContextOptions<ApplicationDbContext> _efCoreOptions = null!;
    private SqlConnectionFactory _connections = null!;
    private Guid _existingId;
    private string _existingEmail = null!;

    [GlobalSetup]
    public void Setup()
    {
        var connectionString = BenchmarkDatabase.Current.ReadsConnectionString;
        _efCoreOptions = BenchmarkDatabase.EfCoreOptions(connectionString);
        _connections = new SqlConnectionFactory(connectionString);

        using var context = new ApplicationDbContext(_efCoreOptions);
        var user = context.Users.AsNoTracking().OrderBy(u => u.CreatedAt).Skip(BenchmarkDatabase.SeededUsers / 2).First();
        _existingId = user.Id;
        _existingEmail = user.Email;
    }

    [Benchmark(Baseline = true, Description = "EFCore")]
    [BenchmarkCategory("UserQueries.GetPage")]
    public async Task<PagedResult<UserDto>> GetPage_EfCore()
    {
        await using var context = new ApplicationDbContext(_efCoreOptions);
        return await new UserQueries(context).GetPageAsync(1, PageSize, CancellationToken.None);
    }

    [Benchmark(Description = "ADO.NET")]
    [BenchmarkCategory("UserQueries.GetPage")]
    public Task<PagedResult<UserDto>> GetPage_Ado() =>
        new AdoUserQueries(_connections).GetPageAsync(1, PageSize, CancellationToken.None);

    [Benchmark(Baseline = true, Description = "EFCore")]
    [BenchmarkCategory("UserQueries.GetById")]
    public async Task<UserDto?> GetById_EfCore()
    {
        await using var context = new ApplicationDbContext(_efCoreOptions);
        return await new UserQueries(context).GetByIdAsync(_existingId, CancellationToken.None);
    }

    [Benchmark(Description = "ADO.NET")]
    [BenchmarkCategory("UserQueries.GetById")]
    public Task<UserDto?> GetById_Ado() =>
        new AdoUserQueries(_connections).GetByIdAsync(_existingId, CancellationToken.None);

    [Benchmark(Baseline = true, Description = "EFCore")]
    [BenchmarkCategory("UserRepository.ExistsByEmail")]
    public async Task<bool> ExistsByEmail_EfCore()
    {
        await using var context = new ApplicationDbContext(_efCoreOptions);
        return await new UserRepository(context).ExistsByEmailAsync(_existingEmail, CancellationToken.None);
    }

    [Benchmark(Description = "ADO.NET")]
    [BenchmarkCategory("UserRepository.ExistsByEmail")]
    public Task<bool> ExistsByEmail_Ado() =>
        new AdoUserRepository(_connections, new AdoChangeSet()).ExistsByEmailAsync(_existingEmail, CancellationToken.None);
}
