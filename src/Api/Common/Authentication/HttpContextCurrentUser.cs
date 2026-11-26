using System.Security.Claims;
using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Infrastructure.Authentication;

namespace CqrsApiExample.Api.Common.Authentication;

/// <summary>
///     <see cref="ICurrentUser"/> read from the request's validated access token, so the
///     Application never sees <c>HttpContext</c> or claims.
/// </summary>
/// <remarks>
///     <para>
///         The JWT bearer handler has already checked the signature, issuer, audience and expiry by
///         the time this is read; it only translates claims (<c>sub</c>, <c>role</c>) into the
///         Application's terms.
///     </para>
///     <para>
///         A singleton that reads the ambient request on each access through <see cref="IHttpContextAccessor"/>. With no request
///         (the outbox background service runs handlers outside one) the user is anonymous.
///     </para>
/// </remarks>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? Id
    {
        get
        {
            string? subject = Principal?.FindFirstValue(AccessTokenClaims.Subject);

            // A valid token always has a GUID subject; anything else is treated as no user.
            bool parsed = Guid.TryParse(subject, out Guid id);

            return IsAuthenticated && parsed ? id : null;
        }
    }

    public bool IsInRole(string role)
    {
        // The bearer options set RoleClaimType to "role", so IsInRole reads that claim.
        return IsAuthenticated && Principal!.IsInRole(role);
    }
}
