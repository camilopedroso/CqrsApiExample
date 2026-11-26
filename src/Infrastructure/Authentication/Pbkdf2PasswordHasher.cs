using System.Globalization;
using System.Security.Cryptography;
using CqrsApiExample.Application.Common.Interfaces.Authentication;

namespace CqrsApiExample.Infrastructure.Authentication;

/// <summary>
///     Hashes passwords with PBKDF2 (HMAC-SHA512, 210,000 iterations, the OWASP recommendation),
///     using only the base class library: no Identity package, so the Infrastructure keeps
///     its distance from ASP.NET Core.
/// </summary>
/// <remarks>
///     <para>
///         A hash is stored as <c>PBKDF2-SHA512.&lt;iterations&gt;.&lt;salt&gt;.&lt;hash&gt;</c>
///         (salt and hash in Base64, which never contains a dot). It describes itself, so the
///         iteration count can be raised later and old hashes still verify with the count they
///         were made with.
///     </para>
///     <para>
///         Slow on purpose (tens of milliseconds per call): it is what makes a stolen table of
///         hashes expensive to crack. The comparison is constant-time, and a missing hash is
///         verified against a dummy one, so neither the result nor the timing says whether an
///         email has an account.
///     </para>
/// </remarks>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Algorithm = "PBKDF2-SHA512";
    private const int Iterations = 210_000;
    private const int SaltSizeInBytes = 16;
    private const int HashSizeInBytes = 32;
    private const char Separator = '.';

    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA512;

    // Verified when there is no stored hash, so that path costs as much as a real check.
    // Built on first use, with a password nobody can type (it is random).
    private static readonly Lazy<string> DummyHash = new(CreateDummyHash);

    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeInBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithm, HashSizeInBytes);

        string saltText = Convert.ToBase64String(salt);
        string hashText = Convert.ToBase64String(hash);
        string iterationsText = Iterations.ToString(CultureInfo.InvariantCulture);

        string passwordHash = string.Join(Separator, Algorithm, iterationsText, saltText, hashText);

        return passwordHash;
    }

    public bool Verify(string password, string? passwordHash)
    {
        // No user (or a user without a password, such as rows created before passwords existed):
        // do the same work against the dummy hash, and fail.
        if (string.IsNullOrEmpty(passwordHash))
        {
            _ = Verify(password, DummyHash.Value);

            return false;
        }

        bool parsed = TryParse(passwordHash, out int iterations, out byte[] salt, out byte[] expectedHash);

        if (!parsed)
        {
            return false;
        }

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithm, expectedHash.Length);

        // Constant-time: a byte-by-byte comparison that stops early would leak, through its
        // timing, how much of the hash an attacker got right.
        bool matches = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);

        return matches;
    }

    private static bool TryParse(string passwordHash, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];

        string[] parts = passwordHash.Split(Separator);

        if (parts is not [Algorithm, var iterationsText, var saltText, var hashText])
        {
            return false;
        }

        bool validIterations = int.TryParse(iterationsText, NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
                               && iterations > 0;

        if (!validIterations)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(saltText);
            hash = Convert.FromBase64String(hashText);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }

    private static string CreateDummyHash()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(SaltSizeInBytes);
        string randomPassword = Convert.ToBase64String(randomBytes);

        Pbkdf2PasswordHasher hasher = new();
        string dummyHash = hasher.Hash(randomPassword);

        return dummyHash;
    }
}
