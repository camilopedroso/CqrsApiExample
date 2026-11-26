using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsApiExample.Infrastructure.Data;

public static class DbInitializer
{
    /// <summary>
    ///     The seed user's password, so a fresh development database has someone to log in as
    ///     (<c>jane.doe@example.com</c>, an administrator). Development only: nothing is seeded anywhere else.
    /// </summary>
    public const string SeedUserPassword = "JaneDoe.Dev.2026";

    private const string DatabaseConnectionStringName = "Database";
    private const string MigrationsConnectionStringName = "Migrations";

    /// <summary>
    ///     Applies pending migrations, creates the API's least-privilege login and seeds the database.
    ///     Meant for local development; in other environments apply migrations as a deployment step
    ///     (e.g. with a migration bundle) and create the login once.
    /// </summary>
    /// <remarks>
    ///     Everything here runs over <c>ConnectionStrings:Migrations</c> (the <c>sa</c> login in
    ///     Development), falling back to <c>ConnectionStrings:Database</c> when there is none. The API
    ///     itself uses <c>Database</c>, whose login may only read and write rows: it couldn't create
    ///     tables, roles or logins.
    /// </remarks>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services,
                                                     CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        string applicationConnectionString = configuration.GetConnectionString(DatabaseConnectionStringName)
                                             ?? throw new InvalidOperationException($"Connection string '{DatabaseConnectionStringName}' not found.");
        string migrationsConnectionString = configuration.GetConnectionString(MigrationsConnectionStringName)
                                            ?? applicationConnectionString;

        // This context only: the rest of the app keeps the least-privilege connection.
        context.Database.SetConnectionString(migrationsConnectionString);

        await context.Database.MigrateAsync(cancellationToken);
        await DevelopmentDatabaseLogin.EnsureAsync(migrationsConnectionString, applicationConnectionString, cancellationToken);
        await SeedAsync(context, passwordHasher, cancellationToken);
    }

    private static async Task SeedAsync(ApplicationDbContext context,
                                        IPasswordHasher passwordHasher,
                                        CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        string passwordHash = passwordHasher.Hash(SeedUserPassword);

        User jane = User.Create("Jane", "Doe", "jane.doe@example.com", passwordHash).Value;

        // The seed user is the administrator, so a fresh database has someone who can list users.
        jane.GrantAdmin();

        context.Users.Add(jane);
        await context.SaveChangesAsync(cancellationToken);
    }
}
