using System.Text;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Domain.Common.Results;

namespace CqrsApiExample.Application.Auth.Commands.Login;

/// <summary>Exchanges an email and password for an access token.</summary>
/// <remarks>
///     <para>
///         <c>AuthController</c> builds it from the <c>LoginRequest</c> body of
///         <c>POST /api/auth/login</c>. <see cref="LoginCommandValidator" /> only checks that both
///         fields are there; <see cref="LoginCommandHandler" /> then checks the password and answers
///         with an <see cref="AccessTokenDto" />, or <c>Auth.InvalidCredentials</c> (401).
///     </para>
///     <para>
///         A command, not a query: it issues a credential, an action with consequences, even though
///         it writes nothing to the database today. Its handler reads the stored hash through
///         <c>IUserQueries</c> (a projection), because it needs no aggregate.
///     </para>
/// </remarks>
/// <param name="Email">The email the user registered with, in any case and with any surrounding spaces.</param>
/// <param name="Password">The plain password. Never logged (the logging decorator only writes the request name).</param>
public sealed record LoginCommand(string Email, string Password) : ICommand<Result<AccessTokenDto>>
{
    // Keeps the password out of the record's generated ToString (see CreateUserCommand).
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Email = {Email}, Password = ***");

        return true;
    }
}
