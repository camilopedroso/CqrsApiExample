using System.Reflection;
using CqrsApiExample.Domain.Common.Models;

namespace CqrsApiExample.Infrastructure.Persistence.Ado;

/// <summary>
///     Fills <see cref="Entity.CreatedAt"/> and <see cref="Entity.CreatedBy"/>, which have private
///     setters, the way EF Core's <c>AuditableEntityInterceptor</c> does through the change
///     tracker. Reflection keeps the domain unaware of persistence.
/// </summary>
internal static class AuditFields
{
    private static readonly PropertyInfo CreatedAt = FindProperty(nameof(Entity.CreatedAt));
    private static readonly PropertyInfo CreatedBy = FindProperty(nameof(Entity.CreatedBy));

    /// <summary>Stamps a new entity with when it was created and by whom (null when anonymous).</summary>
    public static void SetCreated(Entity entity, DateTime at, Guid? by)
    {
        CreatedAt.SetValue(entity, at);
        CreatedBy.SetValue(entity, by);
    }

    private static PropertyInfo FindProperty(string name) =>
        typeof(Entity).GetProperty(name)
        ?? throw new InvalidOperationException($"{nameof(Entity)}.{name} not found.");
}
