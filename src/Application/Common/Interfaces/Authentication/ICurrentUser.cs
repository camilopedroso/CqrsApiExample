namespace CqrsApiExample.Application.Common.Interfaces.Authentication;

/// <summary>Who is making the current request, as far as the Application needs to know.</summary>
/// <remarks>
///     <para>
///         The API implements it from the validated access token (<c>HttpContextCurrentUser</c>), so
///         handlers and decorators never touch <c>HttpContext</c> or claims. Outside a request (the
///         outbox background service, a test) there is no user: <see cref="IsAuthenticated"/> is
///         <see langword="false"/> and <see cref="Id"/> is <see langword="null"/>.
///     </para>
///     <para>
///         Used by <c>AuthorizationDecorator</c> to apply the rules each request declares
///         (<c>IAdminOnlyRequest</c>, <c>IOwnerOrAdminRequest</c>).
///     </para>
/// </remarks>
public interface ICurrentUser
{
    /// <summary>Whether the request carries a valid access token.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The user's id (the token's <c>sub</c> claim), or <see langword="null"/> when anonymous.</summary>
    Guid? Id { get; }

    /// <summary>Whether the token carries <paramref name="role"/> in its <c>role</c> claim.</summary>
    /// <param name="role">
    ///     A role name, as <c>UserRole.ToString()</c> gives it. A name, not the enum, so the API can
    ///     implement this without depending on the domain's user aggregate (an architecture rule).
    /// </param>
    bool IsInRole(string role);
}
