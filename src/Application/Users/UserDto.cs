using CqrsApiExample.Domain.UserAggregate;

namespace CqrsApiExample.Application.Users;

/// <summary>
///     Read model returned by user commands and queries, so the domain
///     entity never leaves the application layer.
/// </summary>
public sealed record UserDto(
    Guid Id,
    string Name,
    string Surname,
    string Email)
{
    public static UserDto From(User user) =>
        new(user.Id, user.Name, user.Surname, user.Email);
}
