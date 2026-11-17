using CqrsApiExample.Application.Users;
using CqrsApiExample.Contracts.User;

namespace CqrsApiExample.Api.Common.Mapping;

public static class UserMappings
{
    public static UserResponse ToResponse(this UserDto user) =>
        new(user.Id, user.Name, user.Surname, user.Email);
}
