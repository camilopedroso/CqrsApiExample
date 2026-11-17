using CqrsApiExample.Domain.Common.Results;
using FluentAssertions;

namespace CqrsApiExample.Domain.UnitTests.Results;

public class ResultTests
{
    private static readonly Error SomeError = Error.Validation("Some.Code", "Some description.");

    [Fact]
    [Trait("Category", "Result")]
    public void Result_From_Value_Should_Be_Success()
    {
        // Act
        Result<int> result = 42;

        // Assert
        result.IsError.Should().BeFalse();
        result.Errors.Should().BeEmpty();
        result.Value.Should().Be(42);
    }

    [Fact]
    [Trait("Category", "Result")]
    public void Result_From_Error_Should_Be_Failure_Without_Value()
    {
        // Act
        Result<int> result = SomeError;

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be(SomeError);
        result.Invoking(r => r.Value).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", "Result")]
    public void Failure_Without_Errors_Should_Throw()
    {
        // Act
        var act = () => Result<int>.Failure([]);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Result")]
    public void Match_Should_Call_The_Matching_Branch()
    {
        // Arrange
        Result<int> success = 1;
        Result<int> failure = SomeError;

        // Act & Assert
        success.Match(value => $"value {value}", errors => "error").Should().Be("value 1");
        failure.Match(value => "value", errors => errors[0].Code).Should().Be(SomeError.Code);
    }
}
