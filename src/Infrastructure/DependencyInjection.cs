using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Application.Common.Interfaces.Notifications;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Infrastructure.Authentication;
using CqrsApiExample.Infrastructure.Notifications;
using CqrsApiExample.Infrastructure.Outbox;
using CqrsApiExample.Infrastructure.Persistence;
using CqrsApiExample.Infrastructure.Persistence.Ado;
using CqrsApiExample.Infrastructure.Persistence.Decorators;
using CqrsApiExample.Infrastructure.Persistence.Instrumentation;
using CqrsApiExample.Infrastructure.Persistence.Interceptors;
using CqrsApiExample.Infrastructure.Persistence.Queries;
using CqrsApiExample.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CqrsApiExample.Infrastructure;

/// <summary>
///     Registers the Infrastructure layer: the clock and email sender
///     (<see cref="AddInfrastructure"/>) and the persistence implementations behind the
///     Application's abstractions, plus the outbox (<see cref="AddPersistence"/>).
/// </summary>
/// <remarks>
///     Called from <c>Program.cs</c> as part of
///     <c>AddPresentation().AddApplication().AddInfrastructure().AddPersistence(config)</c>.
///     The implementations are <c>internal</c>: the other layers only see the interfaces
///     registered here.
/// </remarks>
public static class DependencyInjection
{
    private const string ConnectionStringName = "Database";
    private const string ProviderKey = $"{PersistenceOptions.SectionName}:Provider";

    /// <summary>
    ///     Services that are not persistence: the clock, the email sender, and password hashing
    ///     and token issuing for authentication.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // The clock every layer uses (CreatedAt, outbox schedule, token times); tests replace it with a controllable one.
        services.AddSingleton(TimeProvider.System);

        // Logs the email instead of sending it; the API integration tests replace it with FakeEmailSender.
        services.AddScoped<IEmailSender, LoggingEmailSender>();

        AddAuthentication(services);

        return services;
    }

    /// <summary>
    ///     PBKDF2 password hashing and JWT access tokens. The <c>Jwt</c> section is bound when
    ///     first needed (so tests can add settings afterwards) and validated at startup.
    /// </summary>
    private static void AddAuthentication(IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
                .BindConfiguration(JwtOptions.SectionName)
                .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer),
                          $"{JwtOptions.SectionName}:Issuer is required.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.Audience),
                          $"{JwtOptions.SectionName}:Audience is required.")
                .Validate(options => options.HasValidSigningKey(),
                          $"{JwtOptions.SectionName}:SigningKey must have at least {JwtOptions.MinimumSigningKeyBytes} bytes. " +
                          $"Provide it per environment (e.g. {JwtOptions.SectionName}__SigningKey); it is not in appsettings.json on purpose.")
                .Validate(options => options.AccessTokenLifetime > TimeSpan.Zero,
                          $"{JwtOptions.SectionName}:AccessTokenLifetime must be positive.")
                .ValidateOnStart();

        // Both stateless: one instance for the whole app.
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
    }

    /// <summary>
    ///     Registers both persistence providers (EF Core and ADO.NET), the
    ///     <c>Persistence:Provider</c> flag that picks one, the instrumentation that measures
    ///     it, and the outbox that delivers the domain events.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services,
                                                    IConfiguration configs)
    {
        AddProviderOption(services, configs);
        AddEfCore(services, configs);
        AddAdo(services, configs);
        AddInstrumentationDecorators(services);
        AddOutbox(services);

        return services;
    }

    /// <summary>
    ///     Persistence:Provider (EfCore | Ado) picks the implementation behind the abstractions.
    ///     Read lazily, so settings added after AddPersistence (e.g. by tests) still count;
    ///     ValidateOnStart makes an invalid value stop the app when it starts.
    /// </summary>
    private static void AddProviderOption(IServiceCollection services, IConfiguration configs)
    {
        services.AddOptions<PersistenceOptions>()
                .Configure(ConfigureProvider)
                .ValidateOnStart();

        void ConfigureProvider(PersistenceOptions options)
        {
            string? configuredProvider = configs[ProviderKey];
            options.Provider = ParseProvider(configuredProvider);
        }
    }

    /// <summary>
    ///     EF Core: the DbContext with retries, the audit interceptor and quieter failure logs,
    ///     plus the EF implementations of the repository, unit of work and queries.
    /// </summary>
    private static void AddEfCore(IServiceCollection services, IConfiguration configs)
    {
        // Fills CreatedAt/ModifiedAt on SaveChanges. Stateless, so one instance serves every context.
        services.AddSingleton<AuditableEntityInterceptor>();

        // One DbContext per scope (request), shared by the repository, the unit of work and the queries.
        services.AddDbContext<ApplicationDbContext>(ConfigureDbContext);

        // Registered by their concrete types, not the interfaces: Select (see AddInstrumentationDecorators)
        // picks the EF Core or the ADO.NET one for each interface. Only the picked one is ever created.
        services.AddScoped<UserRepository>();
        services.AddScoped<UnitOfWork>();
        services.AddScoped<UserQueries>();

        void ConfigureDbContext(IServiceProvider serviceProvider, DbContextOptionsBuilder options)
        {
            // Read here, not when AddPersistence runs, so tests can set it afterwards.
            string connectionString = GetConnectionString(configs);
            AuditableEntityInterceptor auditInterceptor =
                serviceProvider.GetRequiredService<AuditableEntityInterceptor>();

            options.UseSqlServer(connectionString, ConfigureSqlServer)
                   .AddInterceptors(auditInterceptor)
                   .ConfigureWarnings(LowerFailureLogs);
        }

        // Retries transient SQL Server failures (e.g. a dropped connection or a failover). Kept
        // short: when the database is really down, the request should fail in seconds, not minutes.
        // AdoRetry uses the same limits.
        static void ConfigureSqlServer(SqlServerDbContextOptionsBuilder sqlServer)
        {
            TimeSpan maxRetryDelay = TimeSpan.FromSeconds(5);

            sqlServer.EnableRetryOnFailure(maxRetryCount: 3,
                                           maxRetryDelay: maxRetryDelay,
                                           errorNumbersToAdd: null);
        }

        // Failed commands either become expected errors (UnitOfWork) or reach the
        // global exception handler, which logs them; EF's own logs would duplicate that.
        static void LowerFailureLogs(WarningsConfigurationBuilder warnings) =>
            warnings.Log((CoreEventId.SaveChangesFailed, LogLevel.Debug),
                         (CoreEventId.QueryIterationFailed, LogLevel.Debug),
                         (RelationalEventId.CommandError, LogLevel.Debug));
    }

    /// <summary>
    ///     ADO.NET: hand-written SQL over the same database and schema (owned by the EF migrations).
    /// </summary>
    private static void AddAdo(IServiceCollection services, IConfiguration configs)
    {
        // A factory, so the connection string is read on first use, like the DbContext's.
        services.AddSingleton(CreateConnectionFactory);

        // The ADO provider's minimal change tracker: the repository adds to it, the unit of work writes it.
        services.AddScoped<AdoChangeSet>();

        // By concrete type, like the EF Core ones; Select picks them when Persistence:Provider is Ado.
        services.AddScoped<AdoUserRepository>();
        services.AddScoped<AdoUnitOfWork>();
        services.AddScoped<AdoUserQueries>();

        SqlConnectionFactory CreateConnectionFactory(IServiceProvider _)
        {
            string connectionString = GetConnectionString(configs);

            return new SqlConnectionFactory(connectionString);
        }
    }

    /// <summary>
    ///     What the application receives: the configured provider, wrapped in the instrumentation
    ///     decorators, so both providers are logged and measured the same way.
    /// </summary>
    private static void AddInstrumentationDecorators(IServiceCollection services)
    {
        // Owns the meter (CqrsApiExample.Persistence) and the duration histogram.
        services.AddSingleton<PersistenceInstrumentation>();

        services.AddScoped<IUserRepository>(CreateUserRepository);
        services.AddScoped<IUnitOfWork>(CreateUnitOfWork);
        services.AddScoped<IUserQueries>(CreateUserQueries);

        static IUserRepository CreateUserRepository(IServiceProvider provider)
        {
            IUserRepository inner = Select<IUserRepository, UserRepository, AdoUserRepository>(provider);
            PersistenceInstrumentation instrumentation = provider.GetRequiredService<PersistenceInstrumentation>();

            return new UserRepositoryInstrumentationDecorator(inner, instrumentation);
        }

        static IUnitOfWork CreateUnitOfWork(IServiceProvider provider)
        {
            IUnitOfWork inner = Select<IUnitOfWork, UnitOfWork, AdoUnitOfWork>(provider);
            PersistenceInstrumentation instrumentation = provider.GetRequiredService<PersistenceInstrumentation>();

            return new UnitOfWorkInstrumentationDecorator(inner, instrumentation);
        }

        static IUserQueries CreateUserQueries(IServiceProvider provider)
        {
            IUserQueries inner = Select<IUserQueries, UserQueries, AdoUserQueries>(provider);
            PersistenceInstrumentation instrumentation = provider.GetRequiredService<PersistenceInstrumentation>();

            return new UserQueriesInstrumentationDecorator(inner, instrumentation);
        }
    }

    /// <summary>
    ///     Domain events are stored with the changes (UnitOfWork) and handled in the background:
    ///     the hosted service runs <see cref="OutboxProcessor"/> in a loop. The processor always
    ///     uses EF Core, whatever the provider.
    /// </summary>
    private static void AddOutbox(IServiceCollection services)
    {
        // Polling interval, batch size, lease and backoff; the defaults live in OutboxOptions.
        services.AddOptions<OutboxOptions>();

        // Scoped: the background service creates a scope (and a DbContext) per batch.
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();
    }

    private static string GetConnectionString(IConfiguration configs)
    {
        string? connectionString = configs.GetConnectionString(ConnectionStringName);

        // appsettings.json has no connection string on purpose: each environment must provide one.
        return connectionString
               ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' not found.");
    }

    private static PersistenceProvider ParseProvider(string? value)
    {
        // Not configured: EF Core is the default.
        if (string.IsNullOrWhiteSpace(value))
        {
            return PersistenceProvider.EfCore;
        }

        bool parsed = Enum.TryParse(value, ignoreCase: true, out PersistenceProvider provider);

        // IsDefined rejects numbers that are not a provider ("5"), which TryParse accepts.
        if (parsed && Enum.IsDefined(provider))
        {
            return provider;
        }

        string validValues = string.Join(", ", Enum.GetNames<PersistenceProvider>());
        string message = $"'{value}' is not a valid {ProviderKey}. Use one of: {validValues}.";

        throw new InvalidOperationException(message);
    }

    /// <summary>
    ///     Resolves the implementation of <typeparamref name="TService"/> for the configured provider.
    ///     Both are registered; only the selected one is created.
    /// </summary>
    private static TService Select<TService, TEfCore, TAdo>(IServiceProvider provider)
        where TService : notnull
        where TEfCore : TService
        where TAdo : TService
    {
        PersistenceOptions options = provider.GetRequiredService<IOptions<PersistenceOptions>>().Value;

        TService service = options.Provider switch
        {
            PersistenceProvider.Ado => provider.GetRequiredService<TAdo>(),
            _ => provider.GetRequiredService<TEfCore>()
        };

        return service;
    }
}
