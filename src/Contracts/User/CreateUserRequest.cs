namespace CqrsApiExample.Contracts.User;

public record CreateUserRequest(string Name,
                                string Surname,
                                string Email);
