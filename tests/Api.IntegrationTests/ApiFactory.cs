using System.Collections.Concurrent;
using CqrsApiExample.Application.Common.Interfaces.Notifications;
using CqrsApiExample.Infrastructure.Outbox;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TheTesting.Shared;

namespace CqrsApiExample.Api.IntegrationTests;

/// <summary>
///     One SQL Server container and one API for the whole assembly. Tests that
///     need different services create their own API with <see cref="CreateFactory"/>,
///     which still uses the same container (with a database of its own).
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly SqlServerFixture _sqlServer = new();

    /// <summary>
    ///     The API with <c>Persistence:Provider=EfCore</c>, set explicitly: appsettings.json selects
    ///     Ado, so without it both endpoint suites would run against ADO.NET.
    /// </summary>
    public ApiFactory Factory { get; private set; } = null!;

    /// <summary>The same API with <c>Persistence:Provider=Ado</c>, on a database of its own.</summary>
    public ApiFactory AdoFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _sqlServer.InitializeAsync();
        Factory = CreateFactory(settings: new Dictionary<string, string?> { ["Persistence:Provider"] = "EfCore" });
        AdoFactory = CreateFactory(settings: new Dictionary<string, string?> { ["Persistence:Provider"] = "Ado" });
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await AdoFactory.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }

    public ApiFactory CreateFactory(Action<IServiceCollection>? configureServices = null,
                                    IReadOnlyDictionary<string, string?>? settings = null)
    {
        return new ApiFactory(_sqlServer.CreateDatabaseConnectionString(), configureServices, settings);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "API";
}

/// <summary>
///     Runs the real API in the Development environment, so migrations and seed
///     are applied at startup, against its own SQL Server database, with emails
///     captured in memory.
/// </summary>
public sealed class ApiFactory(string connectionString,
                               Action<IServiceCollection>? configureServices = null,
                               IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    public FakeEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Database", connectionString);
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IEmailSender>(Emails);
            // Fast outbox, so background effects show up quickly in tests.
            services.Configure<OutboxOptions>(options =>
            {
                options.PollingInterval = TimeSpan.FromMilliseconds(100);
                options.BaseRetryDelay = TimeSpan.FromMilliseconds(200);
            });
            configureServices?.Invoke(services);
        });
    }
}

public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<(string To, string Subject)> _sent = new();
    private readonly ConcurrentDictionary<string, int> _failuresLeft = new();
    private readonly ConcurrentDictionary<string, int> _failedAttempts = new();

    public IReadOnlyCollection<(string To, string Subject)> Sent => _sent;

    /// <summary>Makes the next <paramref name="times"/> sends to this address fail.</summary>
    public void FailFor(string to, int times) => _failuresLeft[to] = times;

    public int FailedAttempts(string to) => _failedAttempts.GetValueOrDefault(to);

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        if (_failuresLeft.TryGetValue(to, out var left) && left > 0)
        {
            _failuresLeft[to] = left - 1;
            _failedAttempts.AddOrUpdate(to, 1, (_, count) => count + 1);
            throw new InvalidOperationException("The email service is unavailable.");
        }

        _sent.Enqueue((to, subject));
        return Task.CompletedTask;
    }
}
