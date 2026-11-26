using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsApiExample.Infrastructure.Data;

public static class DbInitializer
{
    /// <summary>
    ///     The seed user's password, so a fresh development database has someone to log in as
    ///     (<c>jane.doe@example.com</c>). Development only: nothing is seeded anywhere else.
    /// </summary>
    public const string SeedUserPassword = "JaneDoe.Dev.2026";

    /// <summary>
    ///     Applies pending migrations and seeds the database. Meant for local
    ///     development; in other environments apply migrations as a deployment
    ///     step (e.g. with a migration bundle).
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services,
                                                     CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await context.Database.MigrateAsync(cancellationToken);
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

        context.Users.Add(User.Create("Jane", "Doe", "jane.doe@example.com", passwordHash).Value);
        await context.SaveChangesAsync(cancellationToken);
    }
}
