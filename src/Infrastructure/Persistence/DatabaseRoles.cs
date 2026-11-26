namespace CqrsApiExample.Infrastructure.Persistence;

/// <summary>
///     Database roles created by the migrations. The API connects as a user that is only a
///     member of <see cref="Application"/>, never as <c>sa</c> or the owner of the database.
/// </summary>
internal static class DatabaseRoles
{
    /// <summary>
    ///     What the running API may do: read and write rows in its own schemas (<c>Accounts</c>,
    ///     <c>Messaging</c>), nothing else. No DDL, no <c>dbo</c> (so not even the migrations
    ///     history), no server-level rights. If the API is ever tricked into running SQL of an
    ///     attacker's choosing, that SQL can't drop a table or read another database.
    /// </summary>
    /// <remarks>
    ///     Created by the <c>AddApplicationRole</c> migration, so every environment gets the same
    ///     grants. Each environment then adds its own login to it (in Development,
    ///     <c>DevelopmentDatabaseLogin</c> does that). A new schema needs a migration that grants it here.
    /// </remarks>
    public const string Application = "cqrs_api_app_role";
}
