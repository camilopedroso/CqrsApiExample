using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Common.Paging;
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
        var page = new PagedResult<UserDto>([_user], 3, 10, 21);
        _userQueries.Setup(x => x.GetPageAsync(3, 10, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(page);
        var handler = new GetAllUsersQueryHandler(_userQueries.Object);

        // Act
        var result = await handler.Handle(new GetAllUsersQuery(3, 10), default);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeSameAs(page);
        result.Value.TotalPages.Should().Be(3);
    }

    [Theory]
    [Trait("Category", "Validator")]
    [InlineData(0, 20, "Paging.InvalidPage")]
    [InlineData(1, 0, "Paging.InvalidPageSize")]
    [InlineData(1, 101, "Paging.InvalidPageSize")]
    public void GetAllUsersQueryValidator_Should_Keep_Paging_In_Range(int page, int pageSize, string expectedCode)
    {
        // Act
        var result = new GetAllUsersQueryValidator().Validate(new GetAllUsersQuery(page, pageSize));

        // Assert
        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(expectedCode);
    }

    [Fact]
    [Trait("Category", "Validator")]
    public void GetAllUsersQuery_Defaults_To_The_First_Page_Of_20()
    {
        // Act
        var query = new GetAllUsersQuery();

        // Assert
        query.Should().Be(new GetAllUsersQuery(1, 20));
        new GetAllUsersQueryValidator().Validate(query).IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Query")]
    public void PagedResult_Counts_Pages_And_Offsets()
    {
        // Assert
        new PagedResult<UserDto>([], 1, 10, 0).TotalPages.Should().Be(0);
        new PagedResult<UserDto>([], 1, 10, 10).TotalPages.Should().Be(1);
        new PagedResult<UserDto>([], 1, 10, 11).TotalPages.Should().Be(2);
        PagedResult<UserDto>.Offset(3, 10).Should().Be(20);
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
