using CqrsApiExample.Application.Auth;
using CqrsApiExample.Contracts.Auth;

namespace CqrsApiExample.Api.Common.Mapping;

public static class AuthMappings
{
    // The scheme the client must put before the token in the Authorization header.
    private const string BearerTokenType = "Bearer";

    public static AccessTokenResponse ToResponse(this AccessTokenDto accessToken)
    {
        int expiresInSeconds = (int)accessToken.ExpiresIn.TotalSeconds;

        AccessTokenResponse response = new(accessToken.AccessToken, BearerTokenType, expiresInSeconds);

        return response;
    }
}
