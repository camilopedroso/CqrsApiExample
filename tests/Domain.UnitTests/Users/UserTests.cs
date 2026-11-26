using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Domain.UserAggregate.Events;
using FluentAssertions;

namespace CqrsApiExample.Domain.UnitTests.Users;

public class UserTests
{
    [Fact]
    [Trait("Category", "Domain")]
    public void Create_Should_Return_User_With_Normalized_Values()
    {
        // Act
        var result = User.Create("  John ", " Doe ", " John@Doe.COM ", "hashed-password");

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.Name.Should().Be("John");
        result.Value.Surname.Should().Be("Doe");
        result.Value.Email.Should().Be("john@doe.com");
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void Create_Should_Raise_UserCreatedDomainEvent()
    {
        // Act
        var user = User.Create("John", "Doe", "John@Doe.com", "hashed-password").Value;

        // Assert
        user.GetDomainEvents().Should().ContainSingle().Which.Should().Be(
            new UserCreatedDomainEvent(user.Id, "John", "Doe", "john@doe.com"));
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void ClearDomainEvents_Should_Remove_Raised_Events()
    {
        // Arrange
        var user = User.Create("John", "Doe", "john@doe.com", "hashed-password").Value;

        // Act
        user.ClearDomainEvents();

        // Assert
        user.GetDomainEvents().Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void Create_Should_Return_All_Errors_When_Values_Are_Invalid()
    {
        // Act
        var result = User.Create(" ", new string('a', User.SurnameMaxLength + 1), "", "hashed-password");

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().BeEquivalentTo([Errors.Users.InvalidName,
                                               Errors.Users.InvalidSurname,
                                               Errors.Users.InvalidEmail]);
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void Users_With_Same_Data_But_Different_Ids_Should_Not_Be_Equal()
    {
        // Arrange
        var user = User.Create("John", "Doe", "john@doe.com", "hashed-password").Value;
        var otherUser = User.Create("John", "Doe", "john@doe.com", "hashed-password").Value;

        // Assert
        user.Should().NotBe(otherUser);
        (user == otherUser).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void User_Should_Be_Equal_To_Itself()
    {
        // Arrange
        var user = User.Create("John", "Doe", "john@doe.com", "hashed-password").Value;
        var sameUser = user;

        // Assert
        (user == sameUser).Should().BeTrue();
        user.GetHashCode().Should().Be(user.Id.GetHashCode());
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void Create_Should_Keep_The_Password_Hash_Out_Of_The_Event()
    {
        // Act
        var user = User.Create("John", "Doe", "john@doe.com", "hashed-password").Value;

        // Assert
        user.PasswordHash.Should().Be("hashed-password");
        user.GetDomainEvents().Single().Should().BeOfType<UserCreatedDomainEvent>()
            .Which.GetType().GetProperties().Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Password"));
    }

    [Theory]
    [Trait("Category", "Domain")]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_Should_Throw_When_The_Password_Hash_Is_Missing(string passwordHash)
    {
        // Act: a missing hash is a bug in the caller, not invalid input
        var act = () => User.Create("John", "Doe", "john@doe.com", passwordHash);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("passwordHash");
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void Create_Should_Make_A_Member_And_GrantAdmin_Should_Promote_It()
    {
        // Arrange
        var user = User.Create("John", "Doe", "john@doe.com", "hashed-password").Value;

        // Assert: nobody registers as an administrator
        user.Role.Should().Be(UserRole.Member);

        // Act
        user.GrantAdmin();

        // Assert
        user.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void NormalizeEmail_Should_Match_What_Create_Stores()
    {
        // Act
        var user = User.Create("John", "Doe", "  John@Doe.COM ", "hashed-password").Value;

        // Assert
        User.NormalizeEmail("  John@Doe.COM ").Should().Be(user.Email);
    }
}
