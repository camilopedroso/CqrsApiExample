using CqrsApiExample.Application.Users.Commands.CreateUser;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsApiExample.Application.UnitTests.Decorators;

public class ValidationConfigurationTests
{
    [Fact]
    [Trait("Category", "Validator")]
    public void AddApplication_Should_Make_Validation_Messages_English_Regardless_Of_Machine_Culture()
    {
        // Arrange
        using var provider = new ServiceCollection().AddApplication().BuildServiceProvider();
        var validator = provider.GetRequiredService<IValidator<CreateUserCommand>>();

        // Act
        var result = validator.Validate(new CreateUserCommand("", "Doe", "john@doe.com"));

        // Assert
        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("'Name' must not be empty.");
    }
}
