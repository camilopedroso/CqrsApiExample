using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Domain.Common.Results;

namespace CqrsApiExample.Application.Users.Queries.GetAllUsers;

/// <summary>Returns every user as a <see cref="UserDto" />, oldest first.</summary>
/// <remarks>
///     <para>
///         A query handler: it depends only on <see cref="IUserQueries" />, never on the repository
///         or the unit of work (an architecture test enforces it). The read side projects straight
///         to <see cref="UserDto" />, so no <c>User</c> aggregate is built and nothing is tracked.
///     </para>
///     <para>
///         Callers get it wrapped in <c>LoggingDecorator</c> → <c>AuthorizationDecorator</c> → <c>ValidationDecorator</c>, like every
///         handler. The query has no validator, so validation is skipped. The controller maps the list
///         to <c>UserResponse</c> contracts and answers 200.
///     </para>
/// </remarks>
public sealed class GetAllUsersQueryHandler(IUserQueries userQueries) :
    IRequestHandler<GetAllUsersQuery, Result<List<UserDto>>>
{
    public async Task<Result<List<UserDto>>> Handle(
        GetAllUsersQuery request,
        CancellationToken cancellationToken)
    {
        // The query has no parameters, so request is not used. The ordering (by CreatedAt) is
        // done by the database, in both implementations of IUserQueries.
        List<UserDto> users = await userQueries.GetAllAsync(cancellationToken);

        // No users is an empty list, a success, not an error. The list converts implicitly
        // to a successful Result<List<UserDto>>.
        return users;
    }
}
