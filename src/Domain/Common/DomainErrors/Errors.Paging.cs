using CqrsApiExample.Domain.Common.Results;

namespace CqrsApiExample.Domain.Common.DomainErrors;

public static partial class Errors
{
    // Paging limits are an API concern rather than a domain rule, but every error code the API
    // returns lives in Errors, so clients find them all in one place.
    public static class Paging
    {
        public static Error InvalidPage => Error.Validation(code: "Paging.InvalidPage",
                                                            description: "Page must be 1 or greater.",
                                                            field: "Page");

        public static Error InvalidPageSize(int maxPageSize) => Error.Validation(code: "Paging.InvalidPageSize",
                                                                                 description: $"PageSize must be between 1 and {maxPageSize}.",
                                                                                 field: "PageSize");
    }
}
