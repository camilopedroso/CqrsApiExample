using CqrsApiExample.Application.Common.Authorization;
using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.Common.Results;
using CqrsApiExample.Domain.UserAggregate;

namespace CqrsApiExample.Application.Common.Decorators;

/// <summary>
///     Applies the authorization rule a request declares (<see cref="IAdminOnlyRequest"/>,
///     <see cref="IOwnerOrAdminRequest"/>) and returns <c>Auth.Forbidden</c> instead of calling
///     the next handler when the current user may not make it.
/// </summary>
/// <remarks>
///     <para>
///         It sits between <see cref="LoggingDecorator{TRequest, TResult}"/> and
///         <see cref="ValidationDecorator{TRequest, TResult}"/>
///         (<c>Logging → Authorization → Validation → handler</c>): a refused request is still logged
///         with its error code, and a caller that may not make a request learns nothing from its
///         validation errors.
///     </para>
///     <para>
///         The HTTP layer only knows whether there is a valid token (the fallback policy). Which user
///         may see which data is a business rule, so it lives here, next to the use cases, and holds
///         for any caller of the handlers, not only the controllers. Requests without a marker pass
///         straight through (registration, login).
///     </para>
///     <para>
///         Like the validation decorator, it returns a failed result through the static abstract
///         <c>TResult.Failure</c>, never an exception.
///     </para>
/// </remarks>
public sealed class AuthorizationDecorator<TRequest, TResult>(
    IRequestHandler<TRequest, TResult> inner,
    ICurrentUser currentUser) : IRequestHandler<TRequest, TResult>
                                    where TRequest : IRequest<TResult>
                                    where TResult : IFailureResult<TResult>
{
    public async Task<TResult> Handle(TRequest request, CancellationToken cancellationToken)
    {
        Error? error = Authorize(request);

        if (error is not null)
        {
            Error[] errors = [error];

            return TResult.Failure(errors);
        }

        return await inner.Handle(request, cancellationToken);
    }

    /// <summary>The reason the request is refused, or <see langword="null"/> when it may go on.</summary>
    private Error? Authorize(TRequest request)
    {
        bool hasRule = request is IAdminOnlyRequest or IOwnerOrAdminRequest;

        if (!hasRule)
        {
            return null;
        }

        if (!currentUser.IsAuthenticated)
        {
            return Errors.Auth.NotAuthenticated;
        }

        // An administrator passes every rule there is today.
        string adminRole = UserRole.Admin.ToString();

        if (currentUser.IsInRole(adminRole))
        {
            return null;
        }

        bool isOwner = request is IOwnerOrAdminRequest ownedRequest
                       && ownedRequest.OwnerId == currentUser.Id;

        return isOwner ? null : Errors.Auth.Forbidden;
    }
}
