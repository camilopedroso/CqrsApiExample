using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.UserAggregate;
using FluentValidation;

namespace CqrsApiExample.Application.Auth.Commands.Login;

/// <summary>
///     Checks that the email and password are present and not absurdly long. It doesn't
///     check the email format or the password rules: a login only has to match what was
///     registered, and saying more would help someone guessing accounts.
/// </summary>
/// <remarks>
///     The maximum lengths are the registration limits. The one on the password also keeps
///     megabyte-long strings away from the (deliberately slow) password hasher.
/// </remarks>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(Errors.Auth.InvalidEmail.Code)
            .MaximumLength(User.EmailMaxLength).WithErrorCode(Errors.Auth.InvalidEmail.Code);

        RuleFor(x => x.Password)
            .NotEmpty().WithErrorCode(Errors.Auth.InvalidPassword.Code)
            .MaximumLength(User.PasswordMaxLength).WithErrorCode(Errors.Auth.InvalidPassword.Code);
    }
}
