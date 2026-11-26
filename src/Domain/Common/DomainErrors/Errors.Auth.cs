using CqrsApiExample.Domain.Common.Results;
using CqrsApiExample.Domain.UserAggregate;

namespace CqrsApiExample.Domain.Common.DomainErrors;

public static partial class Errors
{
    public static class Auth
    {
        // One error for "no such email" and "wrong password": telling them apart would let
        // anyone find out which emails have an account.
        public static Error InvalidCredentials => Error.Unauthorized(code: "Auth.InvalidCredentials",
                                                                     description: "Email or password is incorrect.");

        // Login input checks. They only say a field is missing or too long, never whether
        // the account exists.
        public static Error InvalidEmail => Error.Validation(code: "Auth.InvalidEmail",
                                                             description: $"Email is required and must have at most {User.EmailMaxLength} characters.",
                                                             field: "Email");
        public static Error InvalidPassword => Error.Validation(code: "Auth.InvalidPassword",
                                                                description: $"Password is required and must have at most {User.PasswordMaxLength} characters.",
                                                                field: "Password");
    }
}
