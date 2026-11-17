using CqrsApiExample.Domain.Common.Models;

namespace CqrsApiExample.Infrastructure.Persistence.Ado;

/// <summary>
///     The ADO.NET provider's minimal change tracker: aggregates added through
///     the repositories in the current scope (request), waiting for
///     <see cref="AdoUnitOfWork.SaveChangesAsync"/>. Only inserts exist today,
///     because the repositories only expose <c>Add</c>.
/// </summary>
/// <remarks>
///     It plays the part of EF Core's <c>ChangeTracker</c>, so both providers follow the same
///     flow: the repository stages, the unit of work commits. Registered as scoped, so
///     <see cref="AdoUserRepository"/> and <see cref="AdoUnitOfWork"/> share the same instance
///     during a request, and nothing leaks into the next one. One scope is used by one request
///     at a time, so it doesn't need to be thread-safe.
///     To support updates or deletes, add a list per kind of change here and the matching
///     UPDATE/DELETE statements in <see cref="AdoUnitOfWork"/>.
/// </remarks>
internal sealed class AdoChangeSet
{
    // New aggregates, in the order they were added; AdoUnitOfWork inserts them in that order.
    private readonly List<AggregateRoot> _added = [];

    /// <summary>Read-only view for the unit of work, which copies it before writing.</summary>
    public IReadOnlyList<AggregateRoot> Added => _added;

    /// <summary>Called by the repositories' <c>Add</c>. Nothing is sent to the database here.</summary>
    public void Add(AggregateRoot aggregate) => _added.Add(aggregate);

    /// <summary>
    ///     Called by the unit of work after a successful commit, so a second
    ///     <c>SaveChangesAsync</c> in the same request doesn't insert the same aggregates again.
    ///     After a failed commit the aggregates stay here.
    /// </summary>
    public void Clear() => _added.Clear();
}
