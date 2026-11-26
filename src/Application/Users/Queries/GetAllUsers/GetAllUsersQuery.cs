using CqrsApiExample.Application.Common.Authorization;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Domain.Common.Results;

namespace CqrsApiExample.Application.Users.Queries.GetAllUsers;

/// <summary>Lists every user, oldest first.</summary>
/// <remarks>
///     <para>
///         It has no parameters, but it is still a type: the request type is what picks the handler
///         (<c>IRequestHandler&lt;GetAllUsersQuery, ...&gt;</c>) and what the logging decorator writes
///         as the request name. <c>UsersController</c> creates it with <c>new()</c> for
///         <c>GET /api/users</c>.
///     </para>
///     <para>
///         A query, so it lives in <c>.Queries.</c> and its handler reads through
///         <c>IUserQueries</c> only. It has no validator (there is no input to check), so the
///         validation decorator passes it straight through.
///     </para>
///     <para>
///         The response is <c>Result&lt;List&lt;UserDto&gt;&gt;</c> even though it never fails today
///         (no users is an empty list, not an error): the decorators require every request to
///         answer with a <c>Result&lt;T&gt;</c>. There is no paging yet, so it returns the whole table.
///     </para>
///     <para>
///         Administrators only (<see cref="IAdminOnlyRequest" />): the list holds every user's email.
///         Anyone else gets <c>Auth.Forbidden</c> (403) from <c>AuthorizationDecorator</c>.
///     </para>
/// </remarks>
public sealed record GetAllUsersQuery : IQuery<Result<List<UserDto>>>, IAdminOnlyRequest;
