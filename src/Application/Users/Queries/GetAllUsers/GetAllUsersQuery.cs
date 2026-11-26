using CqrsApiExample.Application.Common.Authorization;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Application.Common.Paging;
using CqrsApiExample.Domain.Common.Results;

namespace CqrsApiExample.Application.Users.Queries.GetAllUsers;

/// <summary>Lists users, oldest first, one page at a time.</summary>
/// <remarks>
///     <para>
///         <c>UsersController</c> builds it from the query string of <c>GET /api/users?page=&amp;pageSize=</c>.
///         <see cref="GetAllUsersQueryValidator" /> keeps the page at 1 or more and the page size between
///         1 and <see cref="PagedResult{T}.MaxPageSize" />, so no request can pull the whole table.
///     </para>
///     <para>
///         A query, so it lives in <c>.Queries.</c> and its handler reads through <c>IUserQueries</c> only.
///         A page past the last one is an empty page, not an error.
///     </para>
///     <para>
///         Administrators only (<see cref="IAdminOnlyRequest" />): the list holds every user's email.
///         Anyone else gets <c>Auth.Forbidden</c> (403) from <c>AuthorizationDecorator</c>.
///     </para>
/// </remarks>
/// <param name="Page">The page number, starting at 1.</param>
/// <param name="PageSize">How many users per page, at most <see cref="PagedResult{T}.MaxPageSize" />.</param>
public sealed record GetAllUsersQuery(int Page = PagedResult<UserDto>.FirstPage,
                                      int PageSize = PagedResult<UserDto>.DefaultPageSize)
    : IQuery<Result<PagedResult<UserDto>>>, IAdminOnlyRequest;
