using System.Collections.Concurrent;
using System.Reflection;
using CqrsApiExample.Domain.Common.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsApiExample.Application.Common.Events;

/// <summary>
///     Finds the handlers of a domain event and wraps each one in a
///     <see cref="DomainEventHandlerInvocation" />, without running them.
/// </summary>
/// <remarks>
///     <para>
///         The caller is <c>OutboxProcessor</c> (Infrastructure). It runs each invocation on its own
///         and records the ones that finished in <c>OutboxMessageConsumers</c> by
///         <see cref="DomainEventHandlerInvocation.HandlerName" />, so a retry only reruns the
///         handlers that failed. That is why this class hands out invocations instead of
///         calling every handler itself.
///     </para>
///     <para>
///         The outbox deserializes an event as <see cref="IDomainEvent" />, but handlers are
///         registered as <c>IDomainEventHandler&lt;TEvent&gt;</c> for the concrete type
///         (for example <c>UserCreatedDomainEvent</c>), which is only known at run time.
///         <see cref="GetInvocations" /> bridges the two with reflection: it closes the generic
///         <see cref="CreateInvocations{TDomainEvent}" /> over the event's runtime type, and from
///         there on the code is strongly typed again.
///     </para>
///     <para>
///         Registered as scoped, so the handlers come from the outbox's scope, with their own
///         scoped dependencies.
///     </para>
/// </remarks>
public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    // The open generic method CreateInvocations<TDomainEvent>, looked up once for the whole app.
    // It is private and an instance method, hence the binding flags.
    private static readonly MethodInfo CreateInvocationsDefinition =
        typeof(DomainEventDispatcher).GetMethod(
            nameof(CreateInvocations),
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    // One closed method per event type (CreateInvocations<UserCreatedDomainEvent>, ...).
    // MakeGenericMethod is slow, and the outbox polls all the time, so its result is cached.
    // Static, so it outlives the scope the outbox creates on every iteration, and concurrent
    // because scopes can use it at the same time.
    private static readonly ConcurrentDictionary<Type, MethodInfo> CreateInvocationsByEventType = new();

    public IReadOnlyList<DomainEventHandlerInvocation> GetInvocations(IDomainEvent domainEvent)
    {
        // The runtime type, e.g. UserCreatedDomainEvent, not the IDomainEvent of the parameter.
        Type eventType = domainEvent.GetType();

        // The event's runtime type picks the handlers, so close the generic method over it once per type.
        MethodInfo createInvocations = CreateInvocationsByEventType.GetOrAdd(
            eventType,
            type => CreateInvocationsDefinition.MakeGenericMethod(type));

        // Calls CreateInvocations<TEvent>(domainEvent) on this instance. Invoke only resolves the
        // handlers and builds the delegates; no handler runs here, so a handler's exception is
        // thrown later by InvokeAsync, unwrapped, and never as a TargetInvocationException.
        object? invocations = createInvocations.Invoke(this, [domainEvent]);

        return (IReadOnlyList<DomainEventHandlerInvocation>)invocations!;
    }

    // Strongly typed from here on: TDomainEvent is the concrete event type.
    private List<DomainEventHandlerInvocation> CreateInvocations<TDomainEvent>(TDomainEvent domainEvent)
        where TDomainEvent : IDomainEvent
    {
        // Every handler registered for this event (assembly scanning in AddDomainEventHandlers).
        // An event can have none, one or several; none means an empty list, not an error.
        List<IDomainEventHandler<TDomainEvent>> handlers =
            [.. serviceProvider.GetServices<IDomainEventHandler<TDomainEvent>>()];

        List<DomainEventHandlerInvocation> invocations = [];

        foreach (IDomainEventHandler<TDomainEvent> handler in handlers)
        {
            DomainEventHandlerInvocation invocation = CreateInvocation(handler);
            invocations.Add(invocation);
        }

        return invocations;

        // Pairs the handler's name with a delegate that runs it. The delegate captures the handler
        // and the event, so the caller can run it later without knowing TDomainEvent.
        DomainEventHandlerInvocation CreateInvocation(IDomainEventHandler<TDomainEvent> handler)
        {
            // The full type name is the handler's identity in OutboxMessageConsumers: renaming or
            // moving a handler makes pending retries run it again.
            string handlerName = handler.GetType().FullName!;

            DomainEventHandlerInvocation invocation = new(
                handlerName,
                cancellationToken => handler.Handle(domainEvent, cancellationToken));

            return invocation;
        }
    }
}
