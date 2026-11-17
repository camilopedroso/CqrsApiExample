using CqrsApiExample.Domain.Common.Models;

namespace CqrsApiExample.Application.Common.Events;

/// <summary>
///     Turns a domain event into one invocation per handler, without running them.
/// </summary>
/// <remarks>
///     <para>
///         Defined in the Application, next to <see cref="IDomainEventHandler{TEvent}" />, and used
///         by the outbox in the Infrastructure: the outbox knows how to store, lease and retry
///         messages, but not which handlers exist. The implementation is
///         <see cref="DomainEventDispatcher" />.
///     </para>
///     <para>
///         It is not a publisher on purpose. Running the handlers is left to the caller, so each one
///         can succeed, fail and be retried on its own, and a failing handler doesn't make the others
///         run again (delivery is at-least-once per handler).
///     </para>
/// </remarks>
public interface IDomainEventDispatcher
{
    /// <summary>
    ///     One invocation per handler of the event, so the caller can run, track
    ///     and retry each handler on its own.
    /// </summary>
    /// <param name="domainEvent">
    ///     The event as the outbox deserialized it; its runtime type selects the handlers.
    /// </param>
    /// <returns>An empty list when the event has no handlers.</returns>
    IReadOnlyList<DomainEventHandlerInvocation> GetInvocations(IDomainEvent domainEvent);
}
