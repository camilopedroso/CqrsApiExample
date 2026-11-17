using System.Reflection;
using CqrsApiExample.Domain.Common.Models;

namespace CqrsApiExample.Infrastructure.Persistence.Ado;

/// <summary>
///     Fills <see cref="Entity.CreatedAt"/>, which has a private setter, the way
///     EF Core's <c>AuditableEntityInterceptor</c> does through the change
///     tracker. Reflection keeps the domain unaware of persistence.
/// </summary>
internal static class AuditFields
{
    private static readonly PropertyInfo CreatedAt =
        typeof(Entity).GetProperty(nameof(Entity.CreatedAt))
        ?? throw new InvalidOperationException($"{nameof(Entity)}.{nameof(Entity.CreatedAt)} not found.");

    public static void SetCreatedAt(Entity entity, DateTime value) => CreatedAt.SetValue(entity, value);
}
