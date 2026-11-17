using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsApiExample.Infrastructure.Data;

public static class DbInitializer
{
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

        await context.Database.MigrateAsync(cancellationToken);
        await SeedAsync(context, cancellationToken);
    }

    private static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        context.Users.Add(User.Create("Jane", "Doe", "jane.doe@example.com").Value);
        await context.SaveChangesAsync(cancellationToken);
    }
}
