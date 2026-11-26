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

        // Returned by the authorization decorator. The HTTP pipeline already rejects anonymous
        // calls (Request.Unauthorized), so this one only shows up for callers that bypass it.
        public static Error NotAuthenticated => Error.Unauthorized(code: "Auth.NotAuthenticated",
                                                                   description: "This request requires an authenticated user.");

        // The caller is known but the request is about data it may not see. Deliberately the
        // same whether that data exists or not, so a 403 never confirms that an id exists.
        public static Error Forbidden => Error.Forbidden(code: "Auth.Forbidden",
                                                         description: "You are not allowed to do this.");

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
