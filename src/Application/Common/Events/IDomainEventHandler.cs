using CqrsApiExample.Domain.Common.Models;

namespace CqrsApiExample.Application.Common.Events;

/// <summary>
///     Reacts to a domain event after the changes that raised it were saved.
///     Runs from the outbox, so it may run more than once if the process stops
///     right after it: side effects should tolerate that. Implementations are
///     registered by assembly scanning in <c>AddApplication</c>.
/// </summary>
/// <remarks>
///     <para>
///         To react to an event, add a class to the Application project that implements this
///         interface for the concrete event, for example
///         <c>SendWelcomeEmailWhenUserCreated : IDomainEventHandler&lt;UserCreatedDomainEvent&gt;</c>.
///         Nothing else needs registering, and an event can have several handlers.
///     </para>
///     <para>
///         Each handler is tracked on its own by its type's full name
///         (<see cref="DomainEventHandlerInvocation.HandlerName" />): when it finishes, the outbox
///         records it and never runs it again for that message. When it throws, the outbox stops
///         processing that message, logs the exception and schedules a retry with exponential
///         backoff; the retry skips the handlers already recorded, so it runs this one and the
///         ones that hadn't run yet. A handler should therefore just throw on failure, without
///         catching and logging. Renaming or moving it makes pending retries run it again.
///     </para>
///     <para>
///         <c>in</c> makes the interface contravariant, but the container resolves handlers by the
///         exact closed type (<c>IDomainEventHandler&lt;UserCreatedDomainEvent&gt;</c>), so a
///         handler written for a base type or for <see cref="IDomainEvent" /> itself is not called.
///     </para>
/// </remarks>
/// <typeparam name="TDomainEvent">The concrete event type the handler reacts to.</typeparam>
public interface IDomainEventHandler<in TDomainEvent> where TDomainEvent : IDomainEvent
{
    /// <summary>Handles one event. Runs in the outbox's background scope, not in an HTTP request.</summary>
    /// <param name="domainEvent">The event, as deserialized from its outbox row.</param>
    /// <param name="cancellationToken">Cancelled when the application is shutting down.</param>
    Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken);
}
