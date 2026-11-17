namespace CqrsApiExample.Application.Common.Messaging;

/// <summary>A use case input whose handler returns <typeparamref name="TResult"/>.</summary>
/// <remarks>
///     A marker: it has no members. <typeparamref name="TResult" /> is only there to tie a request to
///     its result type, so <see cref="IRequestHandler{TRequest, TResult}" /> can't be declared with a
///     different one (its constraint <c>TRequest : IRequest&lt;TResult&gt;</c> fails to compile).
///     In practice <typeparamref name="TResult" /> is always a <c>Result&lt;T&gt;</c>: the decorators
///     require it, and a handler that breaks that rule stops the app at startup.
///     Requests implement <see cref="ICommand{TResult}" /> or <see cref="IQuery{TResult}" />,
///     not this interface directly.
/// </remarks>
/// <typeparam name="TResult">What the handler returns, e.g. <c>Result&lt;UserDto&gt;</c>.</typeparam>
public interface IRequest<TResult>;
