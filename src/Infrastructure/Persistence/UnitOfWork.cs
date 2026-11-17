using CqrsApiExample.Application.Common.Exceptions;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Domain.Common.Models;
using CqrsApiExample.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CqrsApiExample.Infrastructure.Persistence;

/// <summary>
///     Commits the tracked changes and translates provider-specific failures
///     into exceptions the application layer understands. Domain events raised
///     by the saved aggregates are written to the outbox in the same transaction;
///     <see cref="OutboxProcessor"/> handles them afterwards.
/// </summary>
/// <remarks>
///     EF Core implementation, used when <c>Persistence:Provider</c> is <c>EfCore</c>
///     (the default); <c>AdoUnitOfWork</c> gives the same guarantees with ADO.NET.
///     Scoped like the <see cref="ApplicationDbContext"/> it shares with the repositories,
///     so it commits whatever they tracked during the request. Command handlers call it
///     explicitly; repositories never save.
/// </remarks>
internal sealed class UnitOfWork(ApplicationDbContext context, TimeProvider timeProvider) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        // Turns the pending events into tracked OutboxMessage rows before saving,
        // so the same SaveChanges inserts them along with the aggregates.
        AddDomainEventsToOutbox();

        try
        {
            // A single SaveChanges is a single transaction: the changes and their events
            // are stored together, or not at all.
            // AuditableEntityInterceptor fills CreatedAt/ModifiedAt during this call, and
            // transient SQL failures are retried by EnableRetryOnFailure (see AddPersistence).
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlException
                                           && SqlServerErrors.IsUniqueViolation(sqlException))
        {
            // 2601/2627: a unique index or constraint rejected the row (for example a duplicate
            // email saved by a concurrent request). The Application can't see EF or SqlClient,
            // so it gets its own exception and turns it into an expected error (409).
            // Anything else propagates unchanged to the global exception handler.
            throw new UniqueConstraintViolationException(sqlException.Message, ex);
        }
    }

    private void AddDomainEventsToOutbox()
    {
        // Every tracked aggregate: added, modified or just loaded. Only the ones that
        // raised events produce rows.
        List<AggregateRoot> aggregates = [.. context.ChangeTracker.Entries<AggregateRoot>()
                                                                  .Select(entry => entry.Entity)];

        // Read once, so every message of this commit gets the same timestamp.
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        List<IDomainEvent> domainEvents = [.. aggregates.SelectMany(aggregate => aggregate.GetDomainEvents())];

        foreach (IDomainEvent domainEvent in domainEvents)
        {
            // Stored as (full type name, JSON). The type name is how OutboxProcessor finds the
            // event type again, so renaming or moving an event breaks pending messages.
            (string type, string content) = OutboxSerializer.Serialize(domainEvent);

            OutboxMessage message = new()
            {
                Type = type,
                Content = content,
                OccurredAt = now,
                // Ready right away; after a failure OutboxProcessor moves it forward (backoff).
                NextAttemptAt = now
            };

            context.OutboxMessages.Add(message);
        }

        // Cleared before saving, so a second SaveChangesAsync doesn't add the same events again.
        // If this save fails, the events are gone from the aggregates, but the request fails
        // with it. AdoUnitOfWork clears them only after a successful commit.
        aggregates.ForEach(aggregate => aggregate.ClearDomainEvents());
    }
}
