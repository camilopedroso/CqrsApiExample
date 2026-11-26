using CqrsApiExample.Application.Common.Authorization;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Domain.Common.Results;

namespace CqrsApiExample.Application.Users.Queries.GetUserById;

/// <summary>Finds one user by id.</summary>
/// <remarks>
///     <para>
///         <c>UsersController</c> builds it for <c>GET /api/users/{id}</c>. The route constraint
///         <c>{id:guid}</c> already rejects anything that isn't a GUID (404 <c>Request.NotFound</c>,
///         from routing), so the only invalid value left is <see cref="Guid.Empty" />, which
///         <see cref="GetUserByIdQueryValidator" /> answers with 400 <c>User.InvalidId</c>.
///     </para>
///     <para>
///         A query, so it lives in <c>.Queries.</c> and its handler reads through
///         <c>IUserQueries</c> only. An id that doesn't exist is an expected failure, returned as
///         <c>User.NotFound</c> (404), not an exception.
///     </para>
///     <para>
///         A user may read only itself; an administrator may read anyone
///         (<see cref="IOwnerOrAdminRequest" />). Anyone else gets <c>Auth.Forbidden</c> (403), even for
///         an id that doesn't exist, so the answer never confirms which ids exist. Authorization runs before
///         validation, so only an administrator gets the 400 for <see cref="Guid.Empty" />.
///     </para>
/// </remarks>
/// <param name="Id">The user's id.</param>
public sealed record GetUserByIdQuery(Guid Id) : IQuery<Result<UserDto>>, IOwnerOrAdminRequest
{
    // The user being read is the one the rule protects.
    public Guid OwnerId => Id;
}
