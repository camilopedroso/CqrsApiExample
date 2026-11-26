using CqrsApiExample.Application.Common.Decorators;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Application.Users.Commands.CreateUser;
using CqrsApiExample.Domain.Common.Results;
using FluentAssertions;

namespace CqrsApiExample.Application.UnitTests.Decorators;

public class ValidationDecoratorTests
{
    private readonly UserDto _user = new(Guid.NewGuid(), "John", "Doe", "john@doe.com");

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Handle_Should_Return_Validation_Errors_Without_Calling_The_Handler()
    {
        // Arrange
        var inner = new StubHandler<CreateUserCommand, Result<UserDto>>(_user);
        var decorator = new ValidationDecorator<CreateUserCommand, Result<UserDto>>(inner, new CreateUserCommandValidator());

        // Act
        var result = await decorator.Handle(new CreateUserCommand("", "Doe", "not-an-email", "password123"), default);

        // Assert
        inner.Calls.Should().Be(0);
        result.IsError.Should().BeTrue();
        result.Errors.Should().OnlyContain(error => error.Type == ErrorType.Validation);
        result.Errors.Select(error => (error.Code, error.Field))
              .Should().BeEquivalentTo([("User.InvalidName", "Name"), ("User.InvalidEmail", "Email")]);
    }

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Handle_Should_Call_The_Handler_When_Request_Is_Valid()
    {
        // Arrange
        var inner = new StubHandler<CreateUserCommand, Result<UserDto>>(_user);
        var decorator = new ValidationDecorator<CreateUserCommand, Result<UserDto>>(inner, new CreateUserCommandValidator());

        // Act
        var result = await decorator.Handle(new CreateUserCommand("John", "Doe", "john@doe.com", "password123"), default);

        // Assert
        inner.Calls.Should().Be(1);
        result.Value.Should().Be(_user);
    }

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Handle_Should_Call_The_Handler_When_There_Is_No_Validator()
    {
        // Arrange
        var inner = new StubHandler<CreateUserCommand, Result<UserDto>>(_user);
        var decorator = new ValidationDecorator<CreateUserCommand, Result<UserDto>>(inner);

        // Act
        await decorator.Handle(new CreateUserCommand("", "", "", "password123"), default);

        // Assert
        inner.Calls.Should().Be(1);
    }
}
