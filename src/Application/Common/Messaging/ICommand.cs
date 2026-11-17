namespace CqrsApiExample.Application.Common.Messaging;

/// <summary>Changes state (write side).</summary>
/// <remarks>
///     Its handler uses repositories and commits with <c>IUnitOfWork</c>. It still returns a read
///     model (e.g. <c>UserDto</c>), never a domain entity. An architecture test requires commands
///     to live in a <c>.Commands.</c> namespace and to be named <c>*Command</c>, for example
///     <c>CreateUserCommand : ICommand&lt;Result&lt;UserDto&gt;&gt;</c>.
/// </remarks>
/// <typeparam name="TResult">What the handler returns.</typeparam>
public interface ICommand<TResult> : IRequest<TResult>;
