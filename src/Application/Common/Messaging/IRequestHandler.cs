namespace CqrsApiExample.Application.Common.Messaging;

/// <summary>
///     Handles one request type. Callers depend on this interface directly; the
///     instance they get is wrapped in the decorators registered in
///     <c>AddApplication</c> (logging, then validation).
/// </summary>
/// <remarks>
///     <para>
///         The project's own replacement for MediatR (an architecture test forbids bringing it back).
///         There is no mediator: a controller receives the handler it needs as an action parameter,
///         so every dependency is explicit and "go to implementation" works.
///     </para>
///     <para>
///         The decorators implement this same interface, which is what lets them wrap a handler
///         without the caller noticing. Handlers are found by assembly scanning: adding a class
///         that implements this interface is enough, nothing is registered by hand.
///     </para>
///     <para>
///         A handler returns expected failures (validation, not found, conflict) as errors in the
///         result, and lets unexpected exceptions propagate to the global exception handler.
///     </para>
/// </remarks>
/// <typeparam name="TRequest">The command or query handled.</typeparam>
/// <typeparam name="TResult">The request's result type, taken from <see cref="IRequest{TResult}" />.</typeparam>
public interface IRequestHandler<in TRequest, TResult> where TRequest : IRequest<TResult>
{
    /// <summary>Runs the use case.</summary>
    /// <param name="request">The input. By the time it reaches the real handler, its validator (if it has one) has passed.</param>
    /// <param name="cancellationToken">The request's token, passed down to the database calls.</param>
    Task<TResult> Handle(TRequest request, CancellationToken cancellationToken);
}
