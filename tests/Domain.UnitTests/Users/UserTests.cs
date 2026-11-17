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
        var result = User.Create("  John ", " Doe ", " John@Doe.COM ");

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
        var user = User.Create("John", "Doe", "John@Doe.com").Value;

        // Assert
        user.GetDomainEvents().Should().ContainSingle().Which.Should().Be(
            new UserCreatedDomainEvent(user.Id, "John", "Doe", "john@doe.com"));
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void ClearDomainEvents_Should_Remove_Raised_Events()
    {
        // Arrange
        var user = User.Create("John", "Doe", "john@doe.com").Value;

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
        var result = User.Create(" ", new string('a', User.SurnameMaxLength + 1), "");

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
        var user = User.Create("John", "Doe", "john@doe.com").Value;
        var otherUser = User.Create("John", "Doe", "john@doe.com").Value;

        // Assert
        user.Should().NotBe(otherUser);
        (user == otherUser).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void User_Should_Be_Equal_To_Itself()
    {
        // Arrange
        var user = User.Create("John", "Doe", "john@doe.com").Value;
        var sameUser = user;

        // Assert
        (user == sameUser).Should().BeTrue();
        user.GetHashCode().Should().Be(user.Id.GetHashCode());
    }
}
