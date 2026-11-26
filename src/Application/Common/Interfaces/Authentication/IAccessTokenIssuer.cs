using CqrsApiExample.Application.Auth;

namespace CqrsApiExample.Application.Common.Interfaces.Authentication;

/// <summary>Issues the access token a client sends in <c>Authorization: Bearer</c>.</summary>
/// <remarks>
///     Implemented in the Infrastructure as a signed JWT (<c>JwtAccessTokenIssuer</c>). The API
///     validates the same tokens with the same settings (<c>Jwt</c> section), so whatever this
///     issues is what the API accepts.
/// </remarks>
public interface IAccessTokenIssuer
{
    /// <summary>A token for the user whose credentials were just verified.</summary>
    AccessTokenDto Issue(UserCredentialsDto user);
}
