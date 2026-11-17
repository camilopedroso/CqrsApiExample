using CqrsApiExample.Api.Common.Errors;
using CqrsApiExample.Api.Controllers;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Application.Users.Commands.CreateUser;
using CqrsApiExample.Contracts.User;
using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.Common.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CqrsApiExample.Api.UnitTests;

public class ValidationProblemTests
{
    [Fact]
    [Trait("Category", "CreateUser")]
    public async Task CreateUser_Returns_ValidationProblem_Grouped_By_Field_With_Error_Codes()
    {
        // Arrange
        var handler = new Mock<IRequestHandler<CreateUserCommand, Result<UserDto>>>();
        handler.Setup(h => h.Handle(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Error>
                {
                    Error.Validation(Errors.Users.InvalidName.Code, "'Name' must not be empty.", "Name"),
                    Error.Validation(Errors.Users.InvalidEmail.Code, "'Email' is not a valid email address.", "Email")
                });

        var services = new ServiceCollection();
        services.AddOptions<ApiBehaviorOptions>();
        services.AddSingleton<ProblemDetailsFactory, CqrsApiExampleProblemDetailsFactory>();

        var controller = new UsersController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() }
            }
        };

        // Act
        var result = await controller.CreateUser(new CreateUserRequest("", "Doe", "x"),
                                                 handler.Object,
                                                 CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Keys.Should().BeEquivalentTo(["Name", "Email"]);
        problem.Errors["Email"].Should().Equal("'Email' is not a valid email address.");
        problem.Extensions["errorCodes"].Should().BeAssignableTo<IEnumerable<string>>()
               .Which.Should().Equal(Errors.Users.InvalidName.Code, Errors.Users.InvalidEmail.Code);
    }
}
