using CqrsApiExample.Application.Common.Decorators;
using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Application.Common.Interfaces.Notifications;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Application.Users.Commands.CreateUser;
using CqrsApiExample.Domain.Common.Results;
using CqrsApiExample.Domain.UserAggregate;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CqrsApiExample.Application.UnitTests.Decorators;

/// <summary>The handler registration in AddApplication: every handler, wrapped in the right order.</summary>
public class RequestPipelineTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    [Trait("Category", "Decorator")]
    public void Every_Handler_Is_Registered_Behind_Logging_Then_Authorization_Then_Validation()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var handlerInterfaces = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsGenericTypeDefinition: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(@interface => @interface.IsGenericType
                                 && @interface.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            .ToList();

        // Assert
        handlerInterfaces.Should().HaveCount(4, "CreateUser, GetAllUsers, GetUserById and Login");
        foreach (var handlerInterface in handlerInterfaces)
        {
            var outer = scope.ServiceProvider.GetRequiredService(handlerInterface);
            outer.GetType().GetGenericTypeDefinition().Should().Be(typeof(LoggingDecorator<,>));

            var authorization = InnerHandlerOf(outer);
            authorization.GetType().GetGenericTypeDefinition().Should().Be(typeof(AuthorizationDecorator<,>));

            var validation = InnerHandlerOf(authorization);
            validation.GetType().GetGenericTypeDefinition().Should().Be(typeof(ValidationDecorator<,>));
            InnerHandlerOf(validation).GetType().Name.Should().EndWith("Handler");
        }
    }

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Invalid_Request_Is_Stopped_By_Validation_Before_The_Handler()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<CreateUserCommand, Result<UserDto>>>();

        // Act
        var result = await handler.Handle(new CreateUserCommand("", "Doe", "john@doe.com", "password123"), default);

        // Assert
        result.Errors.Should().ContainSingle().Which.Code.Should().Be("User.InvalidName");
        _userRepository.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Valid_Request_Reaches_The_Handler()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<CreateUserCommand, Result<UserDto>>>();

        // Act
        var result = await handler.Handle(new CreateUserCommand("John", "Doe", "john@doe.com", "password123"), default);

        // Assert
        result.IsError.Should().BeFalse();
        _userRepository.Verify(x => x.Add(It.IsAny<User>()), Times.Once);
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(_userRepository.Object);
        services.AddSingleton(_unitOfWork.Object);
        services.AddSingleton(Mock.Of<IUserQueries>());
        services.AddSingleton(Mock.Of<IEmailSender>());
        services.AddSingleton(Mock.Of<IPasswordHasher>(hasher => hasher.Hash(It.IsAny<string>()) == "hashed-password"));
        services.AddSingleton(Mock.Of<IAccessTokenIssuer>());
        services.AddSingleton(Mock.Of<ICurrentUser>());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    // The decorators keep the wrapped handler in their primary constructor parameter "inner".
    private static object InnerHandlerOf(object decorator)
    {
        var field = decorator.GetType()
                             .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                             .Single(field => field.Name.Contains("inner"));
        return field.GetValue(decorator)!;
    }
}
