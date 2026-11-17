namespace CqrsApiExample.Infrastructure.Persistence;

/// <summary>
///     Database schemas. Every table names its schema in its configuration
///     (<c>ToTable(name, schema)</c>); nothing goes to <c>dbo</c>, which only keeps
///     EF's <c>__EFMigrationsHistory</c>. Raw SQL must qualify table names with
///     these schemas, because an interpolated value in <c>FromSql</c> becomes a
///     parameter, not an identifier.
/// </summary>
internal static class Schemas
{
    /// <summary>User accounts: the users domain.</summary>
    public const string Accounts = "Accounts";

    /// <summary>Messaging infrastructure shared by every area (transactional outbox).</summary>
    public const string Messaging = "Messaging";
}
