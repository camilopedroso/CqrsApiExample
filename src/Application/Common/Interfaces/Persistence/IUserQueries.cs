using CqrsApiExample.Application.Auth;
using CqrsApiExample.Application.Common.Paging;
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
    /// <summary>One page of users, oldest first (ordered by <c>CreatedAt</c>, then <c>Id</c>), and the total count.</summary>
    /// <param name="page">The page number, starting at 1 (already validated).</param>
    /// <param name="pageSize">Users per page (already validated against the maximum).</param>
    /// <param name="cancellationToken">The request's token.</param>
    /// <returns>An empty page when there are no users or the page is past the last one.</returns>
    Task<PagedResult<UserDto>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>One user by id.</summary>
    /// <param name="id">The user's id.</param>
    /// <param name="cancellationToken">The request's token.</param>
    /// <returns>
    ///     <see langword="null" /> when no user has this id. Not an error here: the query handler
    ///     turns it into <c>User.NotFound</c> (404).
    /// </returns>
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The id, email and password hash of the user with this email, for the login.</summary>
    /// <param name="email">
    ///     The normalized email (<c>User.NormalizeEmail</c>), as stored; the lookup is an exact match.
    /// </param>
    /// <param name="cancellationToken">The request's token.</param>
    /// <returns><see langword="null" /> when no user has this email.</returns>
    Task<UserCredentialsDto?> GetCredentialsByEmailAsync(string email, CancellationToken cancellationToken);
}
