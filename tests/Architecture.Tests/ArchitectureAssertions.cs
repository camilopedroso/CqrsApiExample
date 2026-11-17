using FluentAssertions;
using NetArchTest.Rules;

namespace CqrsApiExample.Architecture.Tests;

internal static class ArchitectureAssertions
{
    public static void ShouldPass(this TestResult result, string because)
    {
        var failingTypes = result.FailingTypes?.Select(type => type.FullName) ?? [];
        result.IsSuccessful.Should().BeTrue($"{because}. Violations: {string.Join(", ", failingTypes)}");
    }
}
