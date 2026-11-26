using Microsoft.IdentityModel.JsonWebTokens;

namespace CqrsApiExample.Infrastructure.Authentication;

/// <summary>
///     The claims an access token carries, by name. Shared by the issuer
///     (<c>JwtAccessTokenIssuer</c>) and the API, which reads them back
///     (<c>HttpContextCurrentUser</c>, the bearer options), so both sides agree.
/// </summary>
/// <remarks>
///     The API sets <c>MapInboundClaims = false</c>, so these short names arrive unchanged
///     instead of being renamed to the long WS-* URIs.
/// </remarks>
public static class AccessTokenClaims
{
    /// <summary>The user's id (<c>sub</c>, RFC 7519).</summary>
    public const string Subject = JwtRegisteredClaimNames.Sub;

    /// <summary>A unique id for the token itself (<c>jti</c>, RFC 7519).</summary>
    public const string TokenId = JwtRegisteredClaimNames.Jti;

    /// <summary>The user's role by name (<c>Member</c>, <c>Admin</c>); see <c>UserRole</c>.</summary>
    public const string Role = "role";
}
