using CqrsApiExample.Domain.Common.Results;
using CqrsApiExample.Domain.UserAggregate;

namespace CqrsApiExample.Domain.Common.DomainErrors;

public static partial class Errors
{
    public static class Users
    {
        public static Error UserNotFound => Error.NotFound(code: "User.NotFound",
                                                           description: "User not found.");
        public static Error DuplicateEmail => Error.Conflict(code: "User.DuplicateEmail",
                                                             description: "Email is already in use.");
        public static Error InvalidId => Error.Validation(code: "User.InvalidId",
                                                          description: "Id is required.",
                                                          field: nameof(User.Id));
        public static Error InvalidName => Error.Validation(code: "User.InvalidName",
                                                            description: $"Name is required and must have at most {User.NameMaxLength} characters.",
                                                            field: nameof(User.Name));
        public static Error InvalidSurname => Error.Validation(code: "User.InvalidSurname",
                                                               description: $"Surname is required and must have at most {User.SurnameMaxLength} characters.",
                                                               field: nameof(User.Surname));
        public static Error InvalidEmail => Error.Validation(code: "User.InvalidEmail",
                                                             description: $"Email is required and must have at most {User.EmailMaxLength} characters.",
                                                             field: nameof(User.Email));
    }
}
