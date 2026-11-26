namespace CqrsApiExample.Domain.UserAggregate;

/// <summary>What a user may do beyond its own data.</summary>
/// <remarks>
///     Stored by name (<c>Member</c>, <c>Admin</c>), and sent by name in the access token's
///     <c>role</c> claim, so renaming a value breaks stored rows and issued tokens.
/// </remarks>
public enum UserRole
{
    /// <summary>Every registered user: reads and changes only its own data.</summary>
    Member,

    /// <summary>Can read every user's data.</summary>
    Admin
}
