using CqrsApiExample.Application.Common.Decorators;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Application.Users.Commands.CreateUser;
using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.Common.Results;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace CqrsApiExample.Application.UnitTests.Decorators;

public class LoggingDecoratorTests
{
    private readonly FakeLogger<LoggingDecorator<CreateUserCommand, Result<UserDto>>> _logger = new();
    private readonly CreateUserCommand _command = new("John", "Doe", "john@doe.com", "password123");

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Handle_Should_Log_Information_When_Request_Succeeds()
    {
        // Arrange
        var user = new UserDto(Guid.NewGuid(), "John", "Doe", "john@doe.com");
        var decorator = CreateDecorator(user);

        // Act
        var result = await decorator.Handle(_command, default);

        // Assert
        result.Value.Should().Be(user);
        var record = _logger.Collector.LatestRecord;
        record.Level.Should().Be(LogLevel.Information);
        record.Message.Should().StartWith("CreateUserCommand handled in");
    }

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Handle_Should_Log_Warning_With_Error_Codes_When_Request_Fails()
    {
        // Arrange
        var decorator = CreateDecorator(Errors.Users.DuplicateEmail);

        // Act
        await decorator.Handle(_command, default);

        // Assert
        var record = _logger.Collector.LatestRecord;
        record.Level.Should().Be(LogLevel.Warning);
        record.Message.Should().Contain("CreateUserCommand failed").And.Contain(Errors.Users.DuplicateEmail.Code);
    }

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Handle_Should_Not_Log_Request_Data()
    {
        // Arrange
        var decorator = CreateDecorator(Errors.Users.DuplicateEmail);

        // Act
        await decorator.Handle(_command, default);

        // Assert
        _logger.Collector.GetSnapshot()
               .Should().OnlyContain(record => !record.Message.Contains(_command.Email));
    }

    private LoggingDecorator<CreateUserCommand, Result<UserDto>> CreateDecorator(Result<UserDto> innerResult)
    {
        return new LoggingDecorator<CreateUserCommand, Result<UserDto>>(
            new StubHandler<CreateUserCommand, Result<UserDto>>(innerResult),
            _logger);
    }
}
