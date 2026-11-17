namespace CqrsApiExample.Application.Common.Messaging;

/// <summary>Only reads state (read side).</summary>
/// <remarks>
///     Its handler uses only the <c>I*Queries</c> read interfaces, never a repository (an
///     architecture test enforces it). Queries live in a <c>.Queries.</c> namespace and are named
///     <c>*Query</c>, for example <c>GetUserByIdQuery : IQuery&lt;Result&lt;UserDto&gt;&gt;</c>.
/// </remarks>
/// <typeparam name="TResult">What the handler returns.</typeparam>
public interface IQuery<TResult> : IRequest<TResult>;
