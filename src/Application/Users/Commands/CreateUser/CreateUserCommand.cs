using System.Text;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Domain.Common.Results;

namespace CqrsApiExample.Application.Users.Commands.CreateUser;

/// <summary>Registers a new user.</summary>
/// <remarks>
///     <para>
///         The Application's own input, separate from the API contract: <c>UsersController</c> builds
///         it from the <c>CreateUserRequest</c> body, so the HTTP shape can change without touching
///         the use case.
///     </para>
///     <para>
///         The values arrive exactly as the client sent them. <see cref="CreateUserCommandValidator" />
///         checks their format first (empty, too long, not an email); then
///         <see cref="CreateUserCommandHandler" /> creates the user with <c>User.Create</c>, which
///         trims them, stores the email lowercase and enforces the invariants again.
///     </para>
///     <para>
///         A command, so it lives in <c>.Commands.</c> and its handler writes through the repository
///         and the unit of work. It still answers with a <see cref="UserDto" /> read model, never the
///         <c>User</c> entity. A record because it is an immutable value with nothing but data.
///     </para>
/// </remarks>
/// <param name="Name">The user's first name.</param>
/// <param name="Surname">The user's last name.</param>
/// <param name="Email">The email address; it must not belong to another user (409 otherwise).</param>
/// <param name="Password">
///     The plain password the user will log in with. The handler hashes it; it is never stored
///     or logged as typed.
/// </param>
public sealed record CreateUserCommand(
    string Name,
    string Surname,
    string Email,
    string Password) : ICommand<Result<UserDto>>
{
    // A record's ToString prints every property. This replaces the generated member list so
    // the password never shows up in a log or an exception message by accident.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Name = {Name}, Surname = {Surname}, Email = {Email}, Password = ***");

        return true;
    }
}
