using CqrsApiExample.Api.Controllers;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Application.Common.Paging;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Application.Users.Commands.CreateUser;
using CqrsApiExample.Application.Users.Queries.GetAllUsers;
using CqrsApiExample.Application.Users.Queries.GetUserById;
using CqrsApiExample.Contracts.Common;
using CqrsApiExample.Contracts.User;
using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.Common.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CqrsApiExample.Api.UnitTests;

public class UsersControllerTests
{
    // Each action receives its handler as a parameter, just like [FromServices] does at runtime.
    private readonly UsersController _controller = new();
    private readonly Mock<IRequestHandler<GetAllUsersQuery, Result<PagedResult<UserDto>>>> _getAllUsers = new();
    private readonly Mock<IRequestHandler<GetUserByIdQuery, Result<UserDto>>> _getUserById = new();
    private readonly Mock<IRequestHandler<CreateUserCommand, Result<UserDto>>> _createUser = new();

    [Fact]
    [Trait("Category", "GetUsers")]
    public async Task GetUsers_Returns_Ok_Result_With_UserResponse()
    {
        // Arrange
        List<UserDto> usersList = GetUsers();
        _getAllUsers.Setup(h => h.Handle(new GetAllUsersQuery(2, 5), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new PagedResult<UserDto>(usersList, 2, 5, 12));

        // Act
        var result = await _controller.GetUsers(new PageRequest { Page = 2, PageSize = 5 }, _getAllUsers.Object, CancellationToken.None);

        // Assert
        result.GetType().Should().Be(typeof(OkObjectResult));
        var okResult = (OkObjectResult)result;
        okResult.Should().NotBeNull();
        var page = okResult.Value.Should().BeOfType<PagedResponse<UserResponse>>().Subject;
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(5);
        page.TotalCount.Should().Be(12);
        page.TotalPages.Should().Be(3);
        UserResponse[] users = [.. page.Items];
        users.Should().HaveCount(usersList.Count);
        for (int i = 0; i < usersList.Count; i++)
        {
            users[i].Id.Should().Be(usersList[i].Id);
            users[i].Name.Should().Be(usersList[i].Name);
            users[i].Surname.Should().Be(usersList[i].Surname);
            users[i].Email.Should().Be(usersList[i].Email);
        }
    }

    [Fact]
    [Trait("Category", "GetUsers")]
    public async Task GetUsers_Returns_ProblemResult_With_Errors()
    {
        // Arrange
        _getAllUsers.Setup(h => h.Handle(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Errors.Generic.Ops);

        // Act
        var result = await _controller.GetUsers(new PageRequest { Page = 2, PageSize = 5 }, _getAllUsers.Object, CancellationToken.None);

        // Assert
        result.GetType().Should().Be(typeof(ObjectResult));
        var problemResult = (ObjectResult)result;
        problemResult.Should().NotBeNull();
        problemResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var problemDetails = (ProblemDetails)problemResult.Value!;
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(Errors.Generic.Ops.Description);
        problemDetails.Type.Should().Be(Errors.Generic.Ops.Code);
    }

    [Fact]
    [Trait("Category", "GetUsersById")]
    public async Task GetUserById_Returns_Ok_Result_With_UserResponse()
    {
        // Arrange
        var user = GetUsers().First();
        var query = new GetUserByIdQuery(user.Id);
        _getUserById.Setup(h => h.Handle(query, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(user);

        // Act
        var result = await _controller.GetUser(user.Id, _getUserById.Object, CancellationToken.None);

        // Assert
        result.GetType().Should().Be(typeof(OkObjectResult));
        var okResult = (OkObjectResult)result;
        okResult.Should().NotBeNull();
        okResult.Value.Should().BeAssignableTo<UserResponse>();
        var userResponse = (UserResponse)okResult.Value!;
        userResponse.Id.Should().Be(user.Id);
        userResponse.Name.Should().Be(user.Name);
        userResponse.Surname.Should().Be(user.Surname);
        userResponse.Email.Should().Be(user.Email);
    }

    [Fact]
    [Trait("Category", "GetUsersById")]
    public async Task GetUserById_Returns_BadRequest_ProblemResult_When_Id_Is_Invalid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var query = new GetUserByIdQuery(id);
        _getUserById.Setup(h => h.Handle(query, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Errors.Users.UserNotFound);

        // Act
        var result = await _controller.GetUser(id, _getUserById.Object, CancellationToken.None);

        // Assert
        result.GetType().Should().Be(typeof(ObjectResult));
        var problemResult = (ObjectResult)result;
        problemResult.Should().NotBeNull();
        problemResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var problemDetails = (ProblemDetails)problemResult.Value!;
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(Errors.Users.UserNotFound.Description);
        problemDetails.Type.Should().Be(Errors.Users.UserNotFound.Code);
    }

    [Fact]
    [Trait("Category", "CreateUser")]
    public async Task CreateUser_Returns_CreatedAt_ActionResult_With_UserResponse()
    {
        // Arrange
        var user = GetUsers().First();
        _createUser.Setup(h => h.Handle(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(user);

        // Act
        var result = await _controller.CreateUser(new CreateUserRequest(user.Name,
                                                                        user.Surname,
                                                                        user.Email, "password123"),
                                                        _createUser.Object,
                                                        CancellationToken.None);

        // Assert
        result.GetType().Should().Be(typeof(CreatedAtActionResult));
        var createdAtActionResult = (CreatedAtActionResult)result;
        createdAtActionResult.Should().NotBeNull();
        createdAtActionResult.ActionName.Should().Be(nameof(_controller.GetUser));
        createdAtActionResult.RouteValues.Should().NotBeNull();
        createdAtActionResult.RouteValues.Should().ContainKey("id");
        createdAtActionResult.RouteValues!["id"].Should().Be(user.Id);
        createdAtActionResult.Value.Should().BeAssignableTo<UserResponse>();
        var userResponse = (UserResponse)createdAtActionResult.Value!;
        userResponse.Id.Should().Be(user.Id);
        userResponse.Name.Should().Be(user.Name);
        userResponse.Surname.Should().Be(user.Surname);
        userResponse.Email.Should().Be(user.Email);
    }

    [Fact]
    [Trait("Category", "CreateUser")]
    public async Task CreateUser_Returns_BadRequest_ProblemResult_WithErrors()
    {
        // Arrange
        var user = GetUsers().First();
        _createUser.Setup(h => h.Handle(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Errors.Generic.Ops);

        // Act
        var result = await _controller.CreateUser(new CreateUserRequest(user.Name,
                                                                        user.Surname,
                                                                        user.Email, "password123"),
                                                        _createUser.Object,
                                                        CancellationToken.None);

        // Assert
        result.GetType().Should().Be(typeof(ObjectResult));
        var problemResult = (ObjectResult)result;
        problemResult.Should().NotBeNull();
        problemResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var problemDetails = (ProblemDetails)problemResult.Value!;
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(Errors.Generic.Ops.Description);
        problemDetails.Type.Should().Be(Errors.Generic.Ops.Code);
    }


    private static List<UserDto> GetUsers()
    {
        return [
            new(Guid.NewGuid(), "John", "Doe", "john.doe@example.com"),
            new(Guid.NewGuid(), "Jane", "Smith", "jane.smith@example.com")
        ];
    }
}
