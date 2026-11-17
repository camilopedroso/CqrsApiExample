using CqrsApiExample.Application.Users;

namespace CqrsApiExample.Application.Common.Interfaces.Persistence;

/// <summary>
///     Read side of the user feature: returns read models straight from the
///     database, without loading or tracking aggregates.
/// </summary>
/// <remarks>
///     <para>
///         CQRS split: query handlers depend only on this interface, and command handlers use
///         <see cref="IUserRepository" />. An architecture test forbids queries from using the
///         repository. Reads therefore skip the domain model entirely: no aggregate is built, no
///         invariant runs, and only the columns the <see cref="UserDto" /> needs are selected.
///     </para>
///     <para>
///         Two implementations live in the Infrastructure, chosen by <c>Persistence:Provider</c>:
///         <c>UserQueries</c> (EF Core, <c>AsNoTracking</c> + <c>Select</c>) and
///         <c>AdoUserQueries</c> (hand-written SQL). Both are wrapped by
///         <c>UserQueriesInstrumentationDecorator</c> for logs and metrics.
///     </para>
///     <para>
///         To add a read, add a method here that returns a read model, implement it in both
///         providers, and add it to the decorator.
///     </para>
/// </remarks>
public interface IUserQueries
{
    /// <summary>Every user, oldest first (ordered by <c>CreatedAt</c>).</summary>
    /// <param name="cancellationToken">The request's token.</param>
    /// <returns>An empty list when there are no users.</returns>
    Task<List<UserDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>One user by id.</summary>
    /// <param name="id">The user's id.</param>
    /// <param name="cancellationToken">The request's token.</param>
    /// <returns>
    ///     <see langword="null" /> when no user has this id. Not an error here: the query handler
    ///     turns it into <c>User.NotFound</c> (404).
    /// </returns>
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
