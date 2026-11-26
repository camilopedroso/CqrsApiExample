using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Application.Common.Paging;
using CqrsApiExample.Domain.Common.Results;

namespace CqrsApiExample.Application.Users.Queries.GetAllUsers;

/// <summary>Returns one page of users as <see cref="UserDto" />s, oldest first.</summary>
/// <remarks>
///     <para>
///         A query handler: it depends only on <see cref="IUserQueries" />, never on the repository
///         or the unit of work (an architecture test enforces it). The read side projects straight
///         to <see cref="UserDto" />, so no <c>User</c> aggregate is built and nothing is tracked.
///     </para>
///     <para>
///         Callers get it wrapped in <c>LoggingDecorator</c> → <c>AuthorizationDecorator</c> →
///         <c>ValidationDecorator</c>, so the caller is an administrator and the paging values are in
///         range when <see cref="Handle" /> runs. The controller maps the page to a
///         <c>PagedResponse&lt;UserResponse&gt;</c> and answers 200.
///     </para>
/// </remarks>
public sealed class GetAllUsersQueryHandler(IUserQueries userQueries) :
    IRequestHandler<GetAllUsersQuery, Result<PagedResult<UserDto>>>
{
    public async Task<Result<PagedResult<UserDto>>> Handle(
        GetAllUsersQuery request,
        CancellationToken cancellationToken)
    {
        // The ordering (by CreatedAt), the skip and the count are done by the database,
        // in both implementations of IUserQueries.
        PagedResult<UserDto> page = await userQueries.GetPageAsync(request.Page, request.PageSize, cancellationToken);

        // An empty page (no users, or past the last page) is a success, not an error.
        return page;
    }
}
