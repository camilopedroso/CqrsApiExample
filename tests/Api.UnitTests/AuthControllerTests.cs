using CqrsApiExample.Api.Controllers;
using CqrsApiExample.Application.Auth;
using CqrsApiExample.Application.Auth.Commands.Login;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Contracts.Auth;
using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.Common.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CqrsApiExample.Api.UnitTests;

public class AuthControllerTests
{
    private readonly AuthController _controller = new();
    private readonly Mock<IRequestHandler<LoginCommand, Result<AccessTokenDto>>> _login = new();

    [Fact]
    [Trait("Category", "Login")]
    public async Task Login_Returns_Ok_With_Bearer_Token_Response()
    {
        // Arrange
        _login.Setup(h => h.Handle(new LoginCommand("john@doe.com", "secret-password"), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new AccessTokenDto("a.jwt.token", TimeSpan.FromMinutes(30)));

        // Act
        var result = await _controller.Login(new LoginRequest("john@doe.com", "secret-password"),
                                             _login.Object,
                                             CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(new AccessTokenResponse("a.jwt.token", "Bearer", 1800));
    }

    [Fact]
    [Trait("Category", "Login")]
    public async Task Login_Returns_401_Problem_For_Invalid_Credentials()
    {
        // Arrange
        _login.Setup(h => h.Handle(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Errors.Auth.InvalidCredentials);

        // Act
        var result = await _controller.Login(new LoginRequest("john@doe.com", "wrong"),
                                             _login.Object,
                                             CancellationToken.None);

        // Assert
        var problemResult = result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = problemResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Type.Should().Be(Errors.Auth.InvalidCredentials.Code);
        problem.Title.Should().Be(Errors.Auth.InvalidCredentials.Description);
    }

    [Fact]
    [Trait("Category", "Problem")]
    public async Task Forbidden_Error_Becomes_403_Problem()
    {
        // Arrange
        var forbidden = Error.Forbidden("Test.Forbidden", "Not allowed.");
        _login.Setup(h => h.Handle(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(forbidden);

        // Act
        var result = await _controller.Login(new LoginRequest("john@doe.com", "secret-password"),
                                             _login.Object,
                                             CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
