using CqrsApiExample.Application.Common.Authorization;
using CqrsApiExample.Application.Common.Decorators;
using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.Common.Results;
using CqrsApiExample.Domain.UserAggregate;
using FluentAssertions;
using Moq;

namespace CqrsApiExample.Application.UnitTests.Decorators;

public class AuthorizationDecoratorTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private readonly Mock<ICurrentUser> _currentUser = new();

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Request_Without_A_Rule_Passes_Even_Anonymously()
    {
        // Arrange
        var (decorator, inner) = Build<OpenRequest>();

        // Act
        var result = await decorator.Handle(new OpenRequest(), default);

        // Assert
        result.IsError.Should().BeFalse();
        inner.Calls.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Anonymous_Caller_Gets_NotAuthenticated()
    {
        // Arrange
        var (decorator, inner) = Build<AdminRequest>();

        // Act
        var result = await decorator.Handle(new AdminRequest(), default);

        // Assert
        result.Errors.Should().ContainSingle().Which.Should().Be(Errors.Auth.NotAuthenticated);
        inner.Calls.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Member_Is_Forbidden_From_An_Admin_Request()
    {
        // Arrange
        SignIn(Guid.NewGuid(), UserRole.Member);
        var (decorator, inner) = Build<AdminRequest>();

        // Act
        var result = await decorator.Handle(new AdminRequest(), default);

        // Assert
        result.Errors.Should().ContainSingle().Which.Should().Be(Errors.Auth.Forbidden);
        inner.Calls.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Admin_Passes_Every_Rule()
    {
        // Arrange
        SignIn(Guid.NewGuid(), UserRole.Admin);
        var (adminDecorator, adminInner) = Build<AdminRequest>();
        var (ownedDecorator, ownedInner) = Build<OwnedRequest>();

        // Act
        var adminResult = await adminDecorator.Handle(new AdminRequest(), default);
        var ownedResult = await ownedDecorator.Handle(new OwnedRequest(OwnerId), default);

        // Assert
        adminResult.IsError.Should().BeFalse();
        ownedResult.IsError.Should().BeFalse();
        (adminInner.Calls + ownedInner.Calls).Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Decorator")]
    public async Task Owner_Passes_And_Anyone_Else_Is_Forbidden()
    {
        // Arrange
        var (decorator, inner) = Build<OwnedRequest>();

        // Act
        SignIn(OwnerId, UserRole.Member);
        var asOwner = await decorator.Handle(new OwnedRequest(OwnerId), default);
        SignIn(Guid.NewGuid(), UserRole.Member);
        var asSomeoneElse = await decorator.Handle(new OwnedRequest(OwnerId), default);

        // Assert
        asOwner.IsError.Should().BeFalse();
        asSomeoneElse.Errors.Should().ContainSingle().Which.Should().Be(Errors.Auth.Forbidden);
        inner.Calls.Should().Be(1);
    }

    private void SignIn(Guid id, UserRole role)
    {
        _currentUser.Setup(user => user.IsAuthenticated).Returns(true);
        _currentUser.Setup(user => user.Id).Returns(id);
        _currentUser.Setup(user => user.IsInRole(It.IsAny<string>())).Returns<string>(asked => asked == role.ToString());
    }

    private (AuthorizationDecorator<TRequest, Result<string>> Decorator, CountingHandler<TRequest> Inner) Build<TRequest>()
        where TRequest : IRequest<Result<string>>
    {
        var inner = new CountingHandler<TRequest>();
        var decorator = new AuthorizationDecorator<TRequest, Result<string>>(inner, _currentUser.Object);
        return (decorator, inner);
    }

    private sealed record OpenRequest : IRequest<Result<string>>;

    private sealed record AdminRequest : IRequest<Result<string>>, IAdminOnlyRequest;

    private sealed record OwnedRequest(Guid OwnerId) : IRequest<Result<string>>, IOwnerOrAdminRequest;

    private sealed class CountingHandler<TRequest> : IRequestHandler<TRequest, Result<string>>
        where TRequest : IRequest<Result<string>>
    {
        public int Calls { get; private set; }

        public Task<Result<string>> Handle(TRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Result<string>>("handled");
        }
    }
}
