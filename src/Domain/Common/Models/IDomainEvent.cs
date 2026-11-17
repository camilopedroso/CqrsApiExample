namespace CqrsApiExample.Domain.Common.Models;

/// <summary>
///     Something meaningful that happened in the domain. Raised by aggregates
///     and published by the persistence layer after the changes are saved.
/// </summary>
public interface IDomainEvent;
