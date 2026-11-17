using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.UserAggregate;
using FluentValidation;

namespace CqrsApiExample.Application.Users.Commands.CreateUser;

/// <summary>
///     Checks the input before it reaches the domain. Every rule reuses the
///     code of the matching domain error, so a failure has the same identity
///     whichever layer catches it.
/// </summary>
/// <remarks>
///     <para>
///         Found by assembly scanning (<c>AddValidatorsFromAssembly</c>) and run by
///         <c>ValidationDecorator</c> before <see cref="CreateUserCommandHandler" />. When a rule fails
///         the handler is not called, and the client gets a 400 ValidationProblemDetails whose
///         <c>errors</c> are grouped by field (<c>Name</c>, <c>Surname</c>, <c>Email</c>) and whose
///         <c>errorCodes</c> hold the codes set below.
///     </para>
///     <para>
///         Validation happens twice on purpose: this class checks the input format, and
///         <c>User.Create</c> protects the domain invariants. The limits come from the entity's
///         constants (<c>User.NameMaxLength</c>...), the same ones the EF mapping uses for the column
///         sizes. Two checks live only here: the email format, and the length of the raw value
///         (<c>User.Create</c> measures it after trimming).
///     </para>
///     <para>
///         Every rule must call <c>WithErrorCode</c>, or FluentValidation's default codes
///         (<c>NotEmptyValidator</c>...) would reach the client. The messages are FluentValidation's
///         own, fixed to English in <c>AddApplication</c>.
///     </para>
/// </remarks>
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        // One error per field: stop at the first rule that fails. Without it an empty email
        // would also fail EmailAddress, and the field would carry two errors.
        RuleLevelCascadeMode = CascadeMode.Stop;

        // NotEmpty also rejects whitespace-only strings, like User.Create does.
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(Errors.Users.InvalidName.Code)
            .MaximumLength(User.NameMaxLength).WithErrorCode(Errors.Users.InvalidName.Code);

        RuleFor(x => x.Surname)
            .NotEmpty().WithErrorCode(Errors.Users.InvalidSurname.Code)
            .MaximumLength(User.SurnameMaxLength).WithErrorCode(Errors.Users.InvalidSurname.Code);

        // Rules run in order, so the cheap checks come first. EmailAddress is a loose check
        // (one '@' with text on both sides); only sending an email proves the address exists.
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(Errors.Users.InvalidEmail.Code)
            .MaximumLength(User.EmailMaxLength).WithErrorCode(Errors.Users.InvalidEmail.Code)
            .EmailAddress().WithErrorCode(Errors.Users.InvalidEmail.Code);
    }
}
