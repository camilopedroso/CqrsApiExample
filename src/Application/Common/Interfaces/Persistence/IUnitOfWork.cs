using CqrsApiExample.Application.Common.Exceptions;

namespace CqrsApiExample.Application.Common.Interfaces.Persistence;

/// <summary>
///     Commits, in a single transaction, every change tracked by the
///     repositories in the current scope.
/// </summary>
/// <remarks>
///     <para>
///         Write side only. Repositories (<see cref="IUserRepository" />) add and track aggregates
///         but never save; a command handler decides when the work is done and calls
///         <see cref="SaveChangesAsync" /> once. Query handlers never use it.
///     </para>
///     <para>
///         The same commit stores the domain events raised by the saved aggregates as outbox rows,
///         so the changes and their events exist together or not at all. Handlers never publish
///         events themselves; the outbox delivers them after the commit.
///     </para>
///     <para>
///         Scoped (one per request), like the repositories whose changes it commits. Two
///         implementations live in the Infrastructure, chosen by <c>Persistence:Provider</c>:
///         <c>UnitOfWork</c> (EF Core) and <c>AdoUnitOfWork</c> (ADO.NET), both wrapped by
///         <c>UnitOfWorkInstrumentationDecorator</c> for logs and metrics.
///     </para>
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    ///     Saves the tracked changes and their domain events in one transaction.
    ///     Transient database failures are retried inside the call.
    /// </summary>
    /// <param name="cancellationToken">The request's token; cancelling aborts the commit.</param>
    /// <exception cref="UniqueConstraintViolationException">
    ///     A unique constraint (e.g. a unique index) was violated. It is the only database
    ///     failure translated for the Application: the transaction was rolled back, and a handler
    ///     can catch it and return an expected error (for example <c>User.DuplicateEmail</c>, 409).
    ///     Any other failure propagates to the global exception handler.
    /// </exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
