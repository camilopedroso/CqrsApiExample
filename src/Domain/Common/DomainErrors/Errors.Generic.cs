using CqrsApiExample.Domain.Common.Results;

namespace CqrsApiExample.Domain.Common.DomainErrors;

public static partial class Errors
{
    public static class Generic
    {
        public static Error Ops => Error.Unexpected(code: "General.Unexpected",
                                                    description: "It's embarrassing, but... We'll try to fix that!");
    }
}
