namespace CqrsApiExample.Domain.Common.Results;

public enum ErrorType
{
    Failure,
    Unexpected,
    Validation,
    Conflict,
    NotFound
}

/// <param name="Code">Stable identifier in the <c>Area.Name</c> form, e.g. <c>User.InvalidEmail</c>.</param>
/// <param name="Description">Human-readable message.</param>
/// <param name="Type">Kind of failure; the API maps it to a status code.</param>
/// <param name="Field">Input field the error refers to, when there is one (validation errors).</param>
public sealed record Error(string Code, string Description, ErrorType Type, string? Field = null)
{
    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    public static Error Unexpected(string code, string description) =>
        new(code, description, ErrorType.Unexpected);

    public static Error Validation(string code, string description, string? field = null) =>
        new(code, description, ErrorType.Validation, field);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);
}
