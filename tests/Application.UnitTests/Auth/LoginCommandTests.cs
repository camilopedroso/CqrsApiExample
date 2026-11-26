using CqrsApiExample.Application.Auth;
using CqrsApiExample.Application.Auth.Commands.Login;
using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.UserAggregate;
using FluentAssertions;
using Moq;

namespace CqrsApiExample.Application.UnitTests.Auth;

public class LoginCommandTests
{
    private static readonly UserCredentialsDto Credentials = new(Guid.NewGuid(), "stored-hash", UserRole.Member);
    private static readonly AccessTokenDto Token = new("a.jwt.token", TimeSpan.FromMinutes(30));

    private readonly Mock<IUserQueries> _userQueries = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IAccessTokenIssuer> _tokenIssuer = new();
    private readonly LoginCommandHandler _handler;
    private readonly LoginCommandValidator _validator = new();

    public LoginCommandTests()
    {
        _handler = new LoginCommandHandler(_userQueries.Object, _passwordHasher.Object, _tokenIssuer.Object);
        _tokenIssuer.Setup(x => x.Issue(It.IsAny<UserCredentialsDto>())).Returns(Token);
    }

    [Fact]
    [Trait("Category", "Command")]
    public async Task Handle_Should_Issue_Token_When_Password_Matches()
    {
        // Arrange: the lookup uses the normalized email
        _userQueries.Setup(x => x.GetCredentialsByEmailAsync("john@doe.com", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Credentials);
        _passwordHasher.Setup(x => x.Verify("secret-password", "stored-hash")).Returns(true);

        // Act
        var result = await _handler.Handle(new LoginCommand("  John@Doe.COM ", "secret-password"), default);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(Token);
        _tokenIssuer.Verify(x => x.Issue(Credentials), Times.Once);
    }

    [Fact]
    [Trait("Category", "Command")]
    public async Task Handle_Should_Return_InvalidCredentials_When_Password_Is_Wrong()
    {
        // Arrange
        _userQueries.Setup(x => x.GetCredentialsByEmailAsync("john@doe.com", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Credentials);
        _passwordHasher.Setup(x => x.Verify("wrong", "stored-hash")).Returns(false);

        // Act
        var result = await _handler.Handle(new LoginCommand("john@doe.com", "wrong"), default);

        // Assert
        result.Errors.Should().ContainSingle().Which.Should().Be(Errors.Auth.InvalidCredentials);
        _tokenIssuer.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Category", "Command")]
    public async Task Handle_Should_Return_The_Same_Error_And_Still_Verify_When_Email_Is_Unknown()
    {
        // Arrange: no user; the hasher is asked to verify against no hash (it burns the same time)
        _userQueries.Setup(x => x.GetCredentialsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((UserCredentialsDto?)null);

        // Act
        var result = await _handler.Handle(new LoginCommand("nobody@doe.com", "secret-password"), default);

        // Assert
        result.Errors.Should().ContainSingle().Which.Should().Be(Errors.Auth.InvalidCredentials);
        _passwordHasher.Verify(x => x.Verify("secret-password", null), Times.Once);
        _tokenIssuer.VerifyNoOtherCalls();
    }

    [Theory]
    [Trait("Category", "Validator")]
    [InlineData("", "secret-password", "Auth.InvalidEmail", "Email")]
    [InlineData(null, "secret-password", "Auth.InvalidEmail", "Email")]
    [InlineData("john@doe.com", "", "Auth.InvalidPassword", "Password")]
    [InlineData("john@doe.com", null, "Auth.InvalidPassword", "Password")]
    public void LoginCommandValidator_Should_Require_Both_Fields(string? email,
                                                                 string? password,
                                                                 string expectedCode,
                                                                 string expectedField)
    {
        // Act
        var result = _validator.Validate(new LoginCommand(email!, password!));

        // Assert
        var error = result.Errors.Should().ContainSingle().Subject;
        error.ErrorCode.Should().Be(expectedCode);
        error.PropertyName.Should().Be(expectedField);
    }

    [Fact]
    [Trait("Category", "Validator")]
    public void LoginCommandValidator_Should_Not_Apply_The_Registration_Password_Rules()
    {
        // Act: shorter than the registration minimum, still a valid login attempt
        var result = _validator.Validate(new LoginCommand("not-even-an-email", "short"));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Validator")]
    public void LoginCommandValidator_Should_Reject_Password_Longer_Than_The_Maximum()
    {
        // Act
        var result = _validator.Validate(new LoginCommand("john@doe.com", new string('a', User.PasswordMaxLength + 1)));

        // Assert
        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(Errors.Auth.InvalidPassword.Code);
    }

    [Fact]
    [Trait("Category", "Command")]
    public void ToString_Should_Not_Reveal_The_Password()
    {
        // Act
        var text = new LoginCommand("john@doe.com", "secret-password").ToString();

        // Assert
        text.Should().NotContain("secret-password").And.Contain("Password = ***");
    }
}
