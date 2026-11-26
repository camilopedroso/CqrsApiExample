using CqrsApiExample.Application.Common.Exceptions;
using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Users.Commands.CreateUser;
using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Domain.UserAggregate.Events;
using FluentAssertions;
using Moq;

namespace CqrsApiExample.Application.UnitTests.Users;

public class CreateUserCommandTests
{
    private readonly Mock<IUserRepository> _userRepository;
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly CreateUserCommandHandler _handler;
    private readonly CreateUserCommandValidator _validator;
    private readonly CreateUserCommand _command = new("John", "Doe", "john@doe.com", "password123");

    public CreateUserCommandTests()
    {
        _userRepository = new Mock<IUserRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _handler = new CreateUserCommandHandler(_userRepository.Object,
                                                _unitOfWork.Object,
                                                _passwordHasher.Object);
        _passwordHasher.Setup(x => x.Hash(It.IsAny<string>())).Returns("hashed-password");
        _validator = new();
    }

    [Fact]
    [Trait("Category", "Command")]
    public async Task Handle_Should_Create_User()
    {
        // Arrange
        _userRepository.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(_command, default);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be(_command.Name);
        result.Value.Surname.Should().Be(_command.Surname);
        result.Value.Email.Should().Be(_command.Email);
        // The event travels with the aggregate; the unit of work publishes it after saving.
        _userRepository.Verify(x => x.Add(It.Is<User>(u => u.Id == result.Value.Id
                                                        && u.GetDomainEvents().OfType<UserCreatedDomainEvent>().Any())),
                               Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Command")]
    public async Task Handle_Should_Return_Duplicate_Email_When_Email_Exists()
    {
        // Arrange
        _userRepository.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(_command, default);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be(Errors.Users.DuplicateEmail);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Command")]
    public async Task Handle_Should_Return_Duplicate_Email_When_Unique_Constraint_Is_Violated_On_Save()
    {
        // Arrange: another request inserted the same email between the check and the save
        _userRepository.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(false);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new UniqueConstraintViolationException("UNIQUE constraint failed",
                                                                       new Exception()));

        // Act
        var result = await _handler.Handle(_command, default);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be(Errors.Users.DuplicateEmail);
    }

    [Fact]
    [Trait("Category", "Command")]
    public async Task Handle_Should_Let_Unexpected_Exceptions_Propagate()
    {
        // Arrange: unexpected failures are handled by the global exception handler
        _userRepository.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(false);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidOperationException("Error occurred while saving."));

        // Act
        var act = () => _handler.Handle(_command, default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", "Command")]
    public async Task Handle_Should_Return_Domain_Errors_When_Input_Breaks_Invariants()
    {
        // Act
        var result = await _handler.Handle(new CreateUserCommand(" ", "Doe", "john@doe.com", "password123"), default);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be(Errors.Users.InvalidName);
        _userRepository.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Theory]
    [Trait("Category", "Validator")]
    [InlineData("", "Doe", "john@doe.com", "User.InvalidName", "Name")]
    [InlineData(null, "Doe", "john@doe.com", "User.InvalidName", "Name")]
    [InlineData("John", "", "john@doe.com", "User.InvalidSurname", "Surname")]
    [InlineData("John", null, "john@doe.com", "User.InvalidSurname", "Surname")]
    [InlineData("John", "Doe", "", "User.InvalidEmail", "Email")]
    [InlineData("John", "Doe", null, "User.InvalidEmail", "Email")]
    [InlineData("John", "Doe", "none", "User.InvalidEmail", "Email")]
    public void CreateUserCommandValidator_Should_Return_One_Error_With_Domain_Code(string? name,
                                                                                   string? surname,
                                                                                   string? email,
                                                                                   string expectedCode,
                                                                                   string expectedField)
    {
        // Act
        var result = _validator.Validate(new CreateUserCommand(name!, surname!, email!, "password123"));

        // Assert
        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.ErrorCode.Should().Be(expectedCode);
        error.PropertyName.Should().Be(expectedField);
    }

    [Fact]
    [Trait("Category", "Validator")]
    public void CreateUserCommandValidator_Should_Use_Domain_Max_Length()
    {
        // Act
        var result = _validator.Validate(new CreateUserCommand(new string('a', User.NameMaxLength + 1),
                                                               "Doe",
                                                               "john@doe.com", "password123"));

        // Assert
        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(Errors.Users.InvalidName.Code);
    }

    [Fact]
    [Trait("Category", "Command")]
    public async Task Handle_Should_Store_Only_The_Password_Hash()
    {
        // Arrange
        _userRepository.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(false);

        // Act
        await _handler.Handle(_command, default);

        // Assert: the hasher got the typed password, and the user carries what it returned
        _passwordHasher.Verify(x => x.Hash("password123"), Times.Once);
        _userRepository.Verify(x => x.Add(It.Is<User>(u => u.PasswordHash == "hashed-password")), Times.Once);
    }

    [Theory]
    [Trait("Category", "Validator")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("short")]
    public void CreateUserCommandValidator_Should_Reject_Invalid_Password(string? password)
    {
        // Act
        var result = _validator.Validate(_command with { Password = password! });

        // Assert
        var error = result.Errors.Should().ContainSingle().Subject;
        error.ErrorCode.Should().Be(Errors.Users.InvalidPassword.Code);
        error.PropertyName.Should().Be("Password");
        error.ErrorMessage.Should().Be(Errors.Users.InvalidPassword.Description);
    }

    [Fact]
    [Trait("Category", "Validator")]
    public void CreateUserCommandValidator_Should_Reject_Password_Longer_Than_The_Maximum()
    {
        // Act
        var result = _validator.Validate(_command with { Password = new string('a', User.PasswordMaxLength + 1) });

        // Assert
        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(Errors.Users.InvalidPassword.Code);
    }

    [Fact]
    [Trait("Category", "Command")]
    public void ToString_Should_Not_Reveal_The_Password()
    {
        // Act
        var text = _command.ToString();

        // Assert
        text.Should().NotContain("password123").And.Contain("Password = ***");
    }

    [Fact]
    [Trait("Category", "Validator")]
    public void CreateUserCommandValidator_Should_Accept_Valid_Command()
    {
        // Act & Assert
        _validator.Validate(_command).IsValid.Should().BeTrue();
    }
}
