using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Domain.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CqrsApiExample.Infrastructure.Persistence.Interceptors;

/// <summary>
///     Fills the audit fields (<see cref="Entity.CreatedAt"/>/<see cref="Entity.CreatedBy"/> for new
///     entities, <see cref="Entity.ModifiedAt"/>/<see cref="Entity.ModifiedBy"/> for changed ones)
///     right before changes are saved, so neither the domain nor the handlers deal with audit data.
/// </summary>
/// <remarks>
///     A singleton shared by every <see cref="ApplicationDbContext"/>. That works because
///     <see cref="ICurrentUser"/> is read at save time and its API implementation is request-aware
///     on its own (it reads the ambient <c>HttpContext</c>), so the same instance gives each request
///     its own user. Outside a request the user is anonymous and the "by" fields stay null.
///     <c>AdoUnitOfWork</c> fills the same fields for the ADO.NET provider.
/// </remarks>
internal sealed class AuditableEntityInterceptor(TimeProvider timeProvider, ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
                                                          InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
                                                                          InterceptionResult<int> result,
                                                                          CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // Read once, so every entity in this save gets the same instant and the same user.
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        Guid? userId = currentUser.Id;

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(e => e.CreatedAt).CurrentValue = now;
                entry.Property(e => e.CreatedBy).CurrentValue = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.ModifiedAt).CurrentValue = now;
                entry.Property(e => e.ModifiedBy).CurrentValue = userId;
            }
        }
    }
}
