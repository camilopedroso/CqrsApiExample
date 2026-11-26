using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CqrsApiExample.Infrastructure.Authentication;

/// <summary>
///     The <c>Jwt</c> configuration section: shared by the issuer (<c>JwtAccessTokenIssuer</c>)
///     and by the API, which validates incoming tokens with the same values.
/// </summary>
/// <remarks>
///     <c>appsettings.json</c> has the issuer and audience but no signing key, on purpose (like the
///     connection string): every environment must provide its own, for example in
///     <c>Jwt__SigningKey</c>. Development has a local-only key in <c>appsettings.Development.json</c>.
///     The values are validated when the app starts (<c>ValidateOnStart</c>), so a missing or short
///     key stops it right away instead of failing on the first login.
/// </remarks>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs a key of at least 256 bits.</summary>
    public const int MinimumSigningKeyBytes = 32;

    /// <summary>Who issues the tokens (the <c>iss</c> claim). The API only accepts this issuer.</summary>
    public string Issuer { get; set; } = "";

    /// <summary>Who the tokens are for (the <c>aud</c> claim). The API only accepts this audience.</summary>
    public string Audience { get; set; } = "";

    /// <summary>
    ///     The symmetric key that signs and validates the tokens (HS256). Anyone who has it can
    ///     issue valid tokens, so treat it like a password: keep it out of source control.
    /// </summary>
    public string SigningKey { get; set; } = "";

    /// <summary>
    ///     How long an access token is valid. Short, because a JWT can't be revoked before it
    ///     expires: a stolen token works until then.
    /// </summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>The key built from <see cref="SigningKey"/> (its UTF-8 bytes).</summary>
    public SymmetricSecurityKey CreateSigningKey()
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(SigningKey);
        SymmetricSecurityKey key = new(keyBytes);

        return key;
    }

    internal bool HasValidSigningKey() => Encoding.UTF8.GetByteCount(SigningKey) >= MinimumSigningKeyBytes;
}
