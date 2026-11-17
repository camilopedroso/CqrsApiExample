using CqrsApiExample.Application.Common.Events;
using CqrsApiExample.Application.Common.Interfaces.Notifications;
using CqrsApiExample.Application.Users.Events;
using CqrsApiExample.Domain.UserAggregate.Events;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CqrsApiExample.Application.UnitTests.Events;

/// <summary>
///     Uses the real registration from AddApplication, so it also proves that
///     domain event handlers are found by assembly scanning.
/// </summary>
public class DomainEventDispatcherTests
{
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly UserCreatedDomainEvent _userCreated = new(Guid.NewGuid(), "John", "Doe", "john@doe.com");

    [Fact]
    [Trait("Category", "Events")]
    public async Task GetInvocations_Should_Return_The_Welcome_Email_Handler_For_UserCreated()
    {
        // Arrange
        using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        // Act
        var invocation = dispatcher.GetInvocations(_userCreated).Should().ContainSingle().Subject;
        await invocation.InvokeAsync(default);

        // Assert
        invocation.HandlerName.Should().Be(typeof(SendWelcomeEmailWhenUserCreated).FullName);
        _emailSender.Verify(x => x.SendAsync("john@doe.com",
                                             "Welcome!",
                                             It.Is<string>(body => body.Contains("John")),
                                             It.IsAny<CancellationToken>()),
                            Times.Once);
    }

    [Fact]
    [Trait("Category", "Events")]
    public void GetInvocations_Should_Return_One_Invocation_Per_Handler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IDomainEventHandler<UserCreatedDomainEvent>, FirstHandler>();
        services.AddScoped<IDomainEventHandler<UserCreatedDomainEvent>, SecondHandler>();
        using var provider = services.BuildServiceProvider();

        // Act
        var invocations = new DomainEventDispatcher(provider).GetInvocations(_userCreated);

        // Assert
        invocations.Select(invocation => invocation.HandlerName)
                   .Should().Equal(typeof(FirstHandler).FullName, typeof(SecondHandler).FullName);
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(_emailSender.Object);
        return services.BuildServiceProvider();
    }

    private sealed class FirstHandler : IDomainEventHandler<UserCreatedDomainEvent>
    {
        public Task Handle(UserCreatedDomainEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SecondHandler : IDomainEventHandler<UserCreatedDomainEvent>
    {
        public Task Handle(UserCreatedDomainEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
