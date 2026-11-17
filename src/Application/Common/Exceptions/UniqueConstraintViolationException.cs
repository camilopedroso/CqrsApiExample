namespace CqrsApiExample.Application.Common.Exceptions;

/// <summary>
///     Thrown by the persistence layer when saving would violate a unique
///     constraint. It lets the application react to it without depending on
///     a specific database provider.
/// </summary>
public sealed class UniqueConstraintViolationException(
    string message,
    Exception innerException) : Exception(message, innerException);
