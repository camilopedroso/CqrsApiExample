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
/// </remarks>
/// <param name="Id">The user's id.</param>
public sealed record GetUserByIdQuery(Guid Id) : IQuery<Result<UserDto>>;
