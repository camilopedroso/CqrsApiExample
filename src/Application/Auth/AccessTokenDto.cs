namespace CqrsApiExample.Application.Auth;

/// <summary>An issued access token and how long it stays valid.</summary>
/// <param name="AccessToken">The token itself (a JWT), sent as <c>Authorization: Bearer &lt;token&gt;</c>.</param>
/// <param name="ExpiresIn">Its lifetime from now; after that the client must log in again.</param>
public sealed record AccessTokenDto(string AccessToken, TimeSpan ExpiresIn);
