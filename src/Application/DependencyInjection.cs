using System.Globalization;
using System.Reflection;
using CqrsApiExample.Application.Common.Decorators;
using CqrsApiExample.Application.Common.Events;
using CqrsApiExample.Application.Common.Messaging;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Implementation = (System.Type Interface, System.Type Type);

namespace CqrsApiExample.Application;

/// <summary>
///     Registers the Application layer: request handlers (wrapped in the decorators),
///     validators and domain event handlers. Everything is found by assembly scanning,
///     so adding a class is enough; nothing is registered by hand.
/// </summary>
/// <remarks>
///     Called from <c>Program.cs</c> as part of
///     <c>AddPresentation().AddApplication().AddInfrastructure().AddPersistence(config)</c>.
///     There is no mediator library: this class is what connects a controller asking for
///     <c>IRequestHandler&lt;TRequest, TResult&gt;</c> to the handler that implements it.
/// </remarks>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Every scan below looks only at this assembly (CqrsApiExample.Application).
        Assembly assembly = typeof(DependencyInjection).Assembly;

        AddRequestHandlers(services, assembly);

        // Every IValidator<T> in the assembly; ValidationDecorator picks the one for its request, if any.
        services.AddValidatorsFromAssembly(assembly);

        // Used by the outbox (Infrastructure) to run the handlers of each stored event.
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        AddDomainEventHandlers(services, assembly);

        // Validation messages must not depend on the culture of the machine running the API.
        CultureInfo english = CultureInfo.GetCultureInfo("en");
        ValidatorOptions.Global.LanguageManager.Culture = english;

        return services;
    }

    /// <summary>
    ///     Registers every <see cref="IRequestHandler{TRequest,TResult}"/> wrapped in the decorators,
    ///     outermost first: logging → authorization → validation → handler. The closed decorator types are built here,
    ///     so a handler that breaks their constraints fails at startup, not on its first request.
    /// </summary>
    private static void AddRequestHandlers(IServiceCollection services, Assembly assembly)
    {
        // Innermost first: each decorator wraps the previous one, so the last in the list
        // (LoggingDecorator) is the outermost, the one the controller actually receives.
        // To add a cross-cutting concern, write another decorator and add it here.
        Type[] decoratorsInnermostFirst =
        [
            typeof(ValidationDecorator<,>),
            typeof(AuthorizationDecorator<,>),
            typeof(LoggingDecorator<,>)
        ];

        Type openHandlerInterface = typeof(IRequestHandler<,>);

        // For example (IRequestHandler<CreateUserCommand, Result<UserDto>>, CreateUserCommandHandler).
        List<Implementation> handlers = FindImplementations(assembly, openHandlerInterface);

        foreach (var (handlerInterface, handlerType) in handlers)
        {
            // [TRequest, TResult], e.g. [CreateUserCommand, Result<UserDto>].
            Type[] typeArguments = handlerInterface.GetGenericArguments();

            // Closes the open generics: ValidationDecorator<CreateUserCommand, Result<UserDto>>, ...
            // MakeGenericType checks the decorators' constraints (TResult must implement IFailureResult,
            // which in practice means Result<T>), and runs now, at startup, so a handler that breaks
            // them stops the app from starting.
            Type[] decoratorTypes = [.. decoratorsInnermostFirst.Select(decorator => decorator.MakeGenericType(typeArguments))];

            // The concrete handler is registered under its own type, so the factory below can
            // resolve it with its dependencies (repositories, unit of work...).
            services.AddScoped(handlerType);

            // The interface is registered with a factory that builds the chain once per scope (request).
            // Whoever asks for IRequestHandler<TRequest, TResult> gets the outermost decorator.
            services.AddScoped(handlerInterface, BuildDecoratedHandler);

            // Returns LoggingDecorator → AuthorizationDecorator → ValidationDecorator → the handler.
            object BuildDecoratedHandler(IServiceProvider serviceProvider)
            {
                object handler = serviceProvider.GetRequiredService(handlerType);

                foreach (Type decoratorType in decoratorTypes)
                {
                    // Creates the decorator passing the current handler as its inner handler; the
                    // other constructor parameters (ILogger, ICurrentUser, IValidator...) come from the container.
                    object decorator = ActivatorUtilities.CreateInstance(serviceProvider, decoratorType, handler);
                    handler = decorator;
                }

                return handler;
            }
        }
    }

    /// <summary>
    ///     Registers every <see cref="IDomainEventHandler{TEvent}"/>. One event can have several
    ///     handlers: the dispatcher resolves all of them with <c>GetServices</c>. They are not
    ///     decorated; the outbox records, retries and logs each one.
    /// </summary>
    private static void AddDomainEventHandlers(IServiceCollection services, Assembly assembly)
    {
        Type openEventHandlerInterface = typeof(IDomainEventHandler<>);

        // For example (IDomainEventHandler<UserCreatedDomainEvent>, SendWelcomeEmailWhenUserCreated).
        List<Implementation> eventHandlers = FindImplementations(assembly, openEventHandlerInterface);

        foreach (var (handlerInterface, handlerType) in eventHandlers)
        {
            services.AddScoped(handlerInterface, handlerType);
        }
    }

    /// <summary>
    ///     Concrete classes closing <paramref name="openGenericInterface"/>, as (interface, class) pairs.
    ///     Decorators are open generics, so they are skipped.
    /// </summary>
    /// <remarks>
    ///     A class implementing the interface more than once (for two requests, say) yields one
    ///     pair per closed interface.
    /// </remarks>
    private static List<Implementation> FindImplementations(Assembly assembly, Type openGenericInterface)
    {
        Type[] types = assembly.GetTypes();

        // Lists, not lazy queries, so each step can be inspected in the debugger.
        List<Type> concreteClasses = [.. types.Where(IsConcreteClass)];

        List<Implementation> implementations = [.. concreteClasses.SelectMany(ImplementationsOf)];

        return implementations;

        // Not an interface, abstract class or open generic (such as the decorators).
        static bool IsConcreteClass(Type type) =>
            type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false };

        // IRequestHandler<CreateUserCommand, Result<UserDto>> closes IRequestHandler<,>.
        bool ClosesOpenGenericInterface(Type @interface) =>
            @interface.IsGenericType && @interface.GetGenericTypeDefinition() == openGenericInterface;

        IEnumerable<Implementation> ImplementationsOf(Type type) =>
            type.GetInterfaces()
                .Where(ClosesOpenGenericInterface)
                .Select(@interface => (@interface, type));
    }
}
