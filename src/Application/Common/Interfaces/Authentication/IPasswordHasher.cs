namespace CqrsApiExample.Application.Common.Interfaces.Authentication;

/// <summary>
///     Turns a password into a hash that can be stored, and checks a password against it.
/// </summary>
/// <remarks>
///     The Application only knows this interface; the algorithm (PBKDF2) lives in the
///     Infrastructure (<c>Pbkdf2PasswordHasher</c>). The domain never sees the plain password:
///     <c>CreateUserCommandHandler</c> hashes it and passes the hash to <c>User.Create</c>.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>Hashes <paramref name="password"/> with a new random salt.</summary>
    /// <returns>A self-describing string (algorithm, cost, salt and hash) to store as is.</returns>
    string Hash(string password);

    /// <summary>Whether <paramref name="password"/> matches <paramref name="passwordHash"/>.</summary>
    /// <param name="password">What the client typed.</param>
    /// <param name="passwordHash">
    ///     The stored hash, or <see langword="null"/> when there is no user with that email.
    ///     The check then still does the same expensive work and returns <see langword="false"/>,
    ///     so the response time doesn't reveal whether the email has an account.
    /// </param>
    bool Verify(string password, string? passwordHash);
}
