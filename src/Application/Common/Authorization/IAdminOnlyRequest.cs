namespace CqrsApiExample.Application.Common.Authorization;

/// <summary>A request only administrators may make, such as listing every user.</summary>
/// <remarks>
///     A marker: <c>AuthorizationDecorator</c> answers <c>Auth.Forbidden</c> (403) to anyone else,
///     before the validator or the handler runs. The rule lives on the request type, so reading the
///     request tells you who may send it.
/// </remarks>
public interface IAdminOnlyRequest;
