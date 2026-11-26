namespace CqrsApiExample.Contracts.Auth;

/// <summary>
///     The fields of an OAuth 2.0 token response (RFC 6749, section 5.1), named in this
///     API's camelCase rather than OAuth's snake_case.
/// </summary>
/// <param name="AccessToken">Send it as <c>Authorization: Bearer &lt;accessToken&gt;</c>.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
/// <param name="ExpiresIn">Seconds until the token expires.</param>
public record AccessTokenResponse(string AccessToken,
                                  string TokenType,
                                  int ExpiresIn);
