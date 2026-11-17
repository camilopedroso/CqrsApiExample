namespace CqrsApiExample.Contracts.User;

public record UserResponse(Guid Id,
                           string Name,
                           string Surname,
                           string Email);
