namespace CqrsApiExample.Infrastructure.Persistence;

/// <summary>Which data access implementation serves the persistence abstractions.</summary>
internal enum PersistenceProvider
{
    /// <summary>EF Core: change tracker, LINQ, interceptors (the default).</summary>
    EfCore,

    /// <summary>Plain ADO.NET (Microsoft.Data.SqlClient): hand-written SQL.</summary>
    Ado
}

/// <summary>
///     Bound from the <c>Persistence</c> configuration section, for example
///     <c>"Persistence": { "Provider": "Ado" }</c> or the environment variable
///     <c>Persistence__Provider=Ado</c>. Both providers use the same database and
///     schema; the EF Core migrations own the schema either way.
/// </summary>
internal sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public PersistenceProvider Provider { get; set; } = PersistenceProvider.EfCore;
}
