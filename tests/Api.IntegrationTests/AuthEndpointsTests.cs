using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CqrsApiExample.Contracts.Auth;
using CqrsApiExample.Contracts.User;
using CqrsApiExample.Infrastructure.Authentication;
using CqrsApiExample.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CqrsApiExample.Api.IntegrationTests;

/// <summary>Authentication with the default persistence provider (EF Core).</summary>
[Collection(ApiCollection.Name)]
public sealed class AuthEndpointsTests(ApiFixture api) : AuthEndpointsContract(api.Factory);

/// <summary>The same authentication contract with <c>Persistence:Provider=Ado</c>.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdoAuthEndpointsTests(ApiFixture api) : AuthEndpointsContract(api.AdoFactory);

/// <summary>
///     Register, log in, use the token: the real flow over HTTP, and every way a token
///     can be missing or wrong. Runs once per persistence provider (the login reads the
///     stored hash through it).
/// </summary>
public abstract class AuthEndpointsContract(ApiFactory factory)
{
    private const string Password = "correct horse battery staple";

    private readonly ApiFactory _factory = factory;
    private readonly HttpClient _anonymous = factory.CreateClient();

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Registered_User_Can_Log_In_And_Use_The_Token()
    {
        // Arrange
        var email = NewEmail();
        var registration = await _anonymous.PostAsJsonAsync("/api/users", new CreateUserRequest("John", "Doe", email, Password));
        registration.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: the email is matched after normalization, like at registration
        var login = await _anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest($"  {email.ToUpperInvariant()} ", Password));

        // Assert
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        login.Headers.CacheControl!.NoStore.Should().BeTrue();
        var token = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();
        token!.TokenType.Should().Be("Bearer");
        token.ExpiresIn.Should().Be(30 * 60);

        var created = await registration.Content.ReadFromJsonAsync<UserResponse>();
        var claims = new JsonWebToken(token.AccessToken);
        claims.Subject.Should().Be(created!.Id.ToString());
        claims.GetClaim("role").Value.Should().Be("Member", "registration never grants more than Member");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{created.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var me = await _anonymous.SendAsync(request);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Seeded_User_Can_Log_In_With_The_Development_Password()
    {
        // Act
        var login = await _anonymous.PostAsJsonAsync("/api/auth/login",
                                                     new LoginRequest("jane.doe@example.com", DbInitializer.SeedUserPassword));

        // Assert: the seed user is the administrator
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();
        new JsonWebToken(token!.AccessToken).GetClaim("role").Value.Should().Be("Admin");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Wrong_Password_And_Unknown_Email_Get_The_Same_401()
    {
        // Arrange
        var email = NewEmail();
        (await _anonymous.PostAsJsonAsync("/api/users", new CreateUserRequest("John", "Doe", email, Password)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var wrongPassword = await _anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "wrong password"));
        var unknownEmail = await _anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(NewEmail(), Password));

        // Assert: nothing tells the two apart
        var first = await wrongPassword.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        var second = await unknownEmail.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        first.GetProperty("title").GetString().Should().Be(second.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_Returns_400_When_A_Field_Is_Missing()
    {
        // Act
        var response = await _anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest("", ""));

        // Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Auth.InvalidEmail", "Auth.InvalidPassword");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Register_Returns_400_For_A_Short_Password()
    {
        // Act
        var response = await _anonymous.PostAsJsonAsync("/api/users", new CreateUserRequest("John", "Doe", NewEmail(), "short"));

        // Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "User.InvalidPassword");
        problem.GetProperty("errors").GetProperty("Password")[0].GetString()
               .Should().Be("Password must have between 8 and 128 characters.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Protected_Endpoint_Without_Token_Returns_401_Problem_With_Challenge()
    {
        // Act
        var response = await _anonymous.GetAsync("/api/users");

        // Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Request.Unauthorized");
        response.Headers.WwwAuthenticate.Should().ContainSingle().Which.Scheme.Should().Be("Bearer");
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("expired")]
    [InlineData("wrong-key")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("garbage")]
    public async Task Protected_Endpoint_Rejects_Invalid_Tokens(string kind)
    {
        // Arrange
        var token = kind == "garbage" ? "not.a.token" : CreateToken(kind);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _anonymous.SendAsync(request);

        // Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Request.Unauthorized");
        response.Headers.WwwAuthenticate.Single().Parameter.Should().Contain("invalid_token");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task OpenApi_Document_Declares_The_Bearer_Scheme_On_Protected_Operations_Only()
    {
        // Act: the document itself is anonymous
        var document = await _anonymous.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        // Assert
        var scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        scheme.GetProperty("scheme").GetString().Should().Be("bearer");

        var paths = document.GetProperty("paths");
        paths.GetProperty("/api/Users").GetProperty("get").TryGetProperty("security", out _).Should().BeTrue();
        paths.GetProperty("/api/Users").GetProperty("post").TryGetProperty("security", out _).Should().BeFalse();
        paths.GetProperty("/api/Auth/login").GetProperty("post").TryGetProperty("security", out _).Should().BeFalse();
    }

    /// <summary>A token that is right in every way except the one named by <paramref name="kind"/>.</summary>
    private string CreateToken(string kind)
    {
        var jwt = _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var now = DateTime.UtcNow;
        var key = kind == "wrong-key"
            ? new SymmetricSecurityKey("another-key-that-is-also-long-enough!!"u8.ToArray())
            : jwt.CreateSigningKey();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = kind == "wrong-issuer" ? "someone-else" : jwt.Issuer,
            Audience = kind == "wrong-audience" ? "another-api" : jwt.Audience,
            Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = Guid.NewGuid().ToString() },
            // Expired well beyond the 30 s clock skew.
            IssuedAt = kind == "expired" ? now.AddHours(-2) : now,
            NotBefore = kind == "expired" ? now.AddHours(-2) : now,
            Expires = kind == "expired" ? now.AddHours(-1) : now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@doe.com";
}
