namespace CqrsApiExample.Application.Auth;

/// <summary>
///     What the login needs to know about a user: who it is and its password hash.
/// </summary>
/// <remarks>
///     A read model of its own, separate from <c>UserDto</c>, so the hash never travels through
///     the read models the API returns. Only the login reads it
///     (<c>IUserQueries.GetCredentialsByEmailAsync</c>).
/// </remarks>
/// <param name="Id">The user's id; it becomes the token's <c>sub</c> claim.</param>
/// <param name="PasswordHash">The stored hash, checked with <c>IPasswordHasher.Verify</c>.</param>
public sealed record UserCredentialsDto(Guid Id, string PasswordHash);
