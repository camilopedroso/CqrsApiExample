using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.Common.Results;
using CqrsApiExample.Domain.UserAggregate;

namespace CqrsApiExample.Application.Auth.Commands.Login;

/// <summary>
///     Verifies the password and issues an access token, or answers
///     <c>Auth.InvalidCredentials</c> without saying what was wrong.
/// </summary>
/// <remarks>
///     <para>
///         An unknown email and a wrong password get the same error and take about the same time:
///         the hasher does the full verification even when there is no stored hash. Otherwise the
///         response (or how long it takes) would tell anyone which emails have an account.
///     </para>
///     <para>
///         Callers get it wrapped in <c>LoggingDecorator</c> → <c>ValidationDecorator</c>, so the
///         failed attempts are logged as <c>LoginCommand failed ... with Auth.InvalidCredentials</c>,
///         without the email or the password.
///     </para>
/// </remarks>
public sealed class LoginCommandHandler(
    IUserQueries userQueries,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer) : IRequestHandler<LoginCommand, Result<AccessTokenDto>>
{
    public async Task<Result<AccessTokenDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Stored emails are trimmed and lowercase (User.Create), so the lookup uses the same form.
        string email = User.NormalizeEmail(request.Email);

        UserCredentialsDto? credentials = await userQueries.GetCredentialsByEmailAsync(email, cancellationToken);

        // null when the email is unknown: Verify still does the expensive work, then returns false.
        string? passwordHash = credentials?.PasswordHash;
        bool passwordMatches = passwordHasher.Verify(request.Password, passwordHash);

        if (credentials is null || !passwordMatches)
        {
            return Errors.Auth.InvalidCredentials;
        }

        AccessTokenDto accessToken = accessTokenIssuer.Issue(credentials);

        return accessToken;
    }
}
