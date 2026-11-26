using CqrsApiExample.Infrastructure.Authentication;
using FluentAssertions;

namespace CqrsApiExample.Infrastructure.IntegrationTests.Authentication;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    [Trait("Category", "Authentication")]
    public void Hash_Should_Be_Self_Describing_And_Verify_Only_The_Same_Password()
    {
        // Act
        var hash = _hasher.Hash("correct horse battery staple");

        // Assert
        hash.Should().StartWith("PBKDF2-SHA512.210000.");
        hash.Split('.').Should().HaveCount(4);
        _hasher.Verify("correct horse battery staple", hash).Should().BeTrue();
        _hasher.Verify("Correct horse battery staple", hash).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void Hash_Should_Use_A_New_Salt_Every_Time()
    {
        // Act
        var first = _hasher.Hash("same password");
        var second = _hasher.Hash("same password");

        // Assert: equal passwords don't give away that they are equal
        first.Should().NotBe(second);
        _hasher.Verify("same password", first).Should().BeTrue();
        _hasher.Verify("same password", second).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void Verify_Should_Use_The_Iterations_Stored_In_The_Hash()
    {
        // Arrange: a hash made with fewer iterations (as an older version would have)
        var hash = _hasher.Hash("password123");
        var parts = hash.Split('.');
        var olderHash = string.Join('.', parts[0], "1000", parts[2], parts[3]);

        // Act & Assert: same salt, different cost, so a different hash: it must not verify
        _hasher.Verify("password123", olderHash).Should().BeFalse();
    }

    [Theory]
    [Trait("Category", "Authentication")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("MD5.1.c2FsdA==.aGFzaA==")]
    [InlineData("PBKDF2-SHA512.-5.c2FsdA==.aGFzaA==")]
    [InlineData("PBKDF2-SHA512.1000.not base64!.aGFzaA==")]
    public void Verify_Should_Return_False_For_Missing_Or_Malformed_Hashes(string? storedHash)
    {
        // Act & Assert: never throws, never matches
        _hasher.Verify("password123", storedHash).Should().BeFalse();
    }
}
