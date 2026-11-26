using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.Common.Results;

namespace CqrsApiExample.Application.Users.Queries.GetUserById;

/// <summary>Returns one user as a <see cref="UserDto" />, or <c>User.NotFound</c>.</summary>
/// <remarks>
///     <para>
///         A query handler: it depends only on <see cref="IUserQueries" />, never on the repository
///         or the unit of work (an architecture test enforces it). The read side projects straight
///         to <see cref="UserDto" />, so no <c>User</c> aggregate is built and nothing is tracked.
///     </para>
///     <para>
///         Callers get it wrapped in <c>LoggingDecorator</c> → <c>AuthorizationDecorator</c> → <c>ValidationDecorator</c>, so
///         <see cref="GetUserByIdQueryValidator" /> has already rejected <see cref="Guid.Empty" />
///         when <see cref="Handle" /> runs. The controller answers 200 with the user, or 404 with
///         <c>User.NotFound</c>.
///     </para>
/// </remarks>
public sealed class GetUserByIdQueryHandler(IUserQueries userQueries) :
    IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(
        GetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        // null means "no user with this id": the read side doesn't decide what that means.
        UserDto? user = await userQueries.GetByIdAsync(request.Id, cancellationToken);

        if (user is null)
        {
            // Here it becomes an expected failure, not an exception: the Error converts implicitly
            // to a failed Result<UserDto>, and the controller maps it to 404.
            return Errors.Users.UserNotFound;
        }

        // The UserDto converts implicitly to a successful Result<UserDto>.
        return user;
    }
}
