using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Application.Users.Queries.GetAllUsers;
using CqrsApiExample.Application.Users.Queries.GetUserById;
using CqrsApiExample.Domain.Common.DomainErrors;
using FluentAssertions;
using Moq;

namespace CqrsApiExample.Application.UnitTests.Users;

public class QueryHandlersTests
{
    private readonly Mock<IUserQueries> _userQueries = new();
    private readonly UserDto _user = new(Guid.NewGuid(), "John", "Doe", "john@doe.com");

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetAllUsers_Should_Return_Users_From_Read_Side()
    {
        // Arrange
        _userQueries.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync([_user]);
        var handler = new GetAllUsersQueryHandler(_userQueries.Object);

        // Act
        var result = await handler.Handle(new GetAllUsersQuery(), default);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Equal(_user);
    }

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetUserById_Should_Return_User_When_Found()
    {
        // Arrange
        _userQueries.Setup(x => x.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(_user);
        var handler = new GetUserByIdQueryHandler(_userQueries.Object);

        // Act
        var result = await handler.Handle(new GetUserByIdQuery(_user.Id), default);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(_user);
    }

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetUserById_Should_Return_NotFound_When_Missing()
    {
        // Arrange
        _userQueries.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((UserDto?)null);
        var handler = new GetUserByIdQueryHandler(_userQueries.Object);

        // Act
        var result = await handler.Handle(new GetUserByIdQuery(Guid.NewGuid()), default);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be(Errors.Users.UserNotFound);
    }
}
