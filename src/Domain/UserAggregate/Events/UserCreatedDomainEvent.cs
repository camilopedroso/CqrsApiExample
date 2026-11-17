using CqrsApiExample.Domain.Common.Models;

namespace CqrsApiExample.Domain.UserAggregate.Events;

public sealed record UserCreatedDomainEvent(Guid UserId,
                                            string Name,
                                            string Surname,
                                            string Email) : IDomainEvent;
