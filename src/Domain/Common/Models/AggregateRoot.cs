namespace CqrsApiExample.Domain.Common.Models;

/// <summary>
///     Entry point of a consistency boundary. Only aggregate roots are loaded
///     and saved by repositories, and only they raise domain events.
/// </summary>
/// <remarks>
///     Raising an event only records it in memory, in the aggregate. The domain
///     never publishes anything: when the command handler commits, the unit of work
///     (EF Core or ADO.NET) reads the pending events, writes them to the outbox in the
///     same transaction as the aggregate's changes and clears them. The outbox background
///     service delivers them to their handlers afterwards.
/// </remarks>
public abstract class AggregateRoot : Entity
{
    // Events raised since the aggregate was created or loaded, in the order they happened.
    // Not persisted as part of the aggregate: they only travel to the outbox.
    private readonly List<IDomainEvent> _domainEvents = [];

    // Methods rather than a property, so EF Core never tries to map them.

    /// <summary>
    ///     The events raised and not yet written to the outbox. Returns a copy, so
    ///     callers can't change the aggregate's list.
    /// </summary>
    public IReadOnlyList<IDomainEvent> GetDomainEvents() => [.. _domainEvents];

    /// <summary>
    ///     Called by the unit of work once the events are in the outbox, so a later
    ///     save of the same aggregate doesn't write them again.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    ///     Records that something happened to this aggregate. Protected: only the
    ///     aggregate itself decides which events it raises (see <c>User.Create</c>).
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
