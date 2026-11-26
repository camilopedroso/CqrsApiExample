using CqrsApiExample.Application.Auth;
using CqrsApiExample.Application.Common.Interfaces.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CqrsApiExample.Infrastructure.Authentication;

/// <summary>
///     Issues access tokens as JWTs signed with HMAC-SHA256, with the issuer, audience,
///     key and lifetime from <see cref="JwtOptions"/>.
/// </summary>
/// <remarks>
///     The token carries as little as possible: who the user is (<c>sub</c>), a unique id
///     (<c>jti</c>) and the standard times. A JWT is only signed, not encrypted, so anyone
///     holding it can read its claims; personal data stays out of it.
///     The times come from <see cref="TimeProvider"/>, so tests can issue tokens at any instant.
/// </remarks>
internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    // Thread-safe and stateless: one instance serves every call.
    private static readonly JsonWebTokenHandler TokenHandler = new();

    public AccessTokenDto Issue(UserCredentialsDto user)
    {
        JwtOptions jwt = options.Value;

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        DateTime expiresAt = now + jwt.AccessTokenLifetime;

        SymmetricSecurityKey signingKey = jwt.CreateSigningKey();
        SigningCredentials signingCredentials = new(signingKey, SecurityAlgorithms.HmacSha256);

        Dictionary<string, object> claims = new()
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
        };

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = signingCredentials
        };

        string token = TokenHandler.CreateToken(descriptor);

        AccessTokenDto accessToken = new(token, jwt.AccessTokenLifetime);

        return accessToken;
    }
}
