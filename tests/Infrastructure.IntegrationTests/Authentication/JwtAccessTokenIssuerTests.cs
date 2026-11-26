using CqrsApiExample.Application.Auth;
using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CqrsApiExample.Infrastructure.IntegrationTests.Authentication;

public class JwtAccessTokenIssuerTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "a-test-signing-key-that-is-long-enough",
        AccessTokenLifetime = TimeSpan.FromMinutes(15)
    };

    private readonly TestClock _clock = new(TestDatabase.Start);

    [Fact]
    [Trait("Category", "Authentication")]
    public async Task Issue_Should_Create_A_Signed_Token_With_The_User_Id_And_Lifetime()
    {
        // Arrange
        var issuer = new JwtAccessTokenIssuer(Microsoft.Extensions.Options.Options.Create(Options), _clock);
        var userId = Guid.NewGuid();

        // Act
        var accessToken = issuer.Issue(new UserCredentialsDto(userId, "unused-hash", UserRole.Admin));

        // Assert: valid for the configured issuer, audience and key, at the issuing instant
        accessToken.ExpiresIn.Should().Be(TimeSpan.FromMinutes(15));
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.AccessToken,
                                                                            ValidationParameters(at: TestDatabase.Start));
        validation.IsValid.Should().BeTrue(validation.Exception?.Message);

        var token = new JsonWebToken(accessToken.AccessToken);
        token.Subject.Should().Be(userId.ToString());
        token.GetClaim("role").Value.Should().Be("Admin");
        token.Id.Should().NotBeNullOrEmpty();
        token.Alg.Should().Be(SecurityAlgorithms.HmacSha256);
        token.ValidTo.Should().Be(TestDatabase.Start.UtcDateTime.AddMinutes(15));
        token.Claims.Select(claim => claim.Type).Should().NotContain(["email", "name", "password"]);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void AddInfrastructure_Should_Reject_A_Missing_Or_Short_Signing_Key()
    {
        // Arrange: appsettings.json has no key on purpose
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "issuer",
            ["Jwt:Audience"] = "audience",
            ["Jwt:SigningKey"] = "too-short"
        });

        // Act
        var act = () => provider.GetRequiredService<IOptions<JwtOptions>>().Value;

        // Assert
        act.Should().Throw<OptionsValidationException>()
           .WithMessage("*Jwt:SigningKey must have at least 32 bytes*");
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void AddInfrastructure_Should_Bind_The_Jwt_Section()
    {
        // Arrange
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "issuer",
            ["Jwt:Audience"] = "audience",
            ["Jwt:SigningKey"] = Options.SigningKey,
            ["Jwt:AccessTokenLifetime"] = "00:05:00"
        });

        // Act
        var options = provider.GetRequiredService<IOptions<JwtOptions>>().Value;

        // Assert
        options.Issuer.Should().Be("issuer");
        options.AccessTokenLifetime.Should().Be(TimeSpan.FromMinutes(5));
    }

    private static TokenValidationParameters ValidationParameters(DateTimeOffset at) => new()
    {
        ValidIssuer = Options.Issuer,
        ValidAudience = Options.Audience,
        IssuerSigningKey = Options.CreateSigningKey(),
        LifetimeValidator = (notBefore, expires, _, _) => notBefore <= at.UtcDateTime && at.UtcDateTime < expires
    };

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new ServiceCollection().AddSingleton<IConfiguration>(configuration)
                                      .AddInfrastructure()
                                      .BuildServiceProvider();
    }
}
