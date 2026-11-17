namespace CqrsApiExample.Application.Common.Events;

/// <summary>A single handler ready to handle one domain event.</summary>
/// <remarks>
///     The handler and the event are already captured in <see cref="InvokeAsync" />, so the caller
///     (the outbox) can run it without knowing the event's concrete type.
///     <see cref="IDomainEventDispatcher.GetInvocations" /> creates one per handler.
/// </remarks>
/// <param name="HandlerName">
///     Stable name of the handler (its type's full name), used to remember that it already ran.
///     The outbox stores it in <c>OutboxMessageConsumers</c> and skips it on a retry.
/// </param>
/// <param name="InvokeAsync">Runs the handler for the event.</param>
public sealed record DomainEventHandlerInvocation(
    string HandlerName,
    Func<CancellationToken, Task> InvokeAsync);
