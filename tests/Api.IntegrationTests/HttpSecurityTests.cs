using System.Net;
using System.Net.Http.Json;
using CqrsApiExample.Application.Auth;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Common.Paging;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Contracts.Auth;
using CqrsApiExample.Domain.UserAggregate;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsApiExample.Api.IntegrationTests;

/// <summary>
///     The HTTP-level protections: security headers on every response, CORS for configured
///     origins only, and rate limits that answer 429 with Retry-After.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class HttpSecurityTests(ApiFixture api)
{
    private const string AllowedOrigin = "https://app.example.com";

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("/api/users")]           // 401 written by the status code pages
    [InlineData("/openapi/v1.json")]     // 200
    public async Task Every_Response_Carries_The_Security_Headers(string path)
    {
        // Arrange
        using var client = api.Factory.CreateClient();

        // Act
        var response = await client.GetAsync(path);

        // Assert
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'none'");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Security_Headers_Survive_An_Unexpected_Error()
    {
        // Arrange: the exception handler clears the response headers before writing the 500
        await using var factory = api.CreateFactory(services =>
            services.AddScoped<IUserQueries, ThrowingQueries>());
        using var client = factory.CreateAuthenticatedClient(role: UserRole.Admin);

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Swagger_UI_Gets_No_Content_Security_Policy()
    {
        // Arrange
        using var client = api.Factory.CreateClient();

        // Act: the page needs its scripts
        var response = await client.GetAsync("/swagger/index.html");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Content-Security-Policy").Should().BeFalse();
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Cors_Answers_Preflights_From_Allowed_Origins_Only()
    {
        // Arrange
        await using var factory = api.CreateFactory(settings: new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = AllowedOrigin
        });
        using var client = factory.CreateClient();

        // Act
        var allowed = await client.SendAsync(Preflight(AllowedOrigin));
        var other = await client.SendAsync(Preflight("https://evil.example.com"));

        // Assert
        allowed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        allowed.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal(AllowedOrigin);
        allowed.Headers.GetValues("Access-Control-Allow-Headers").Single().ToLowerInvariant().Should().Contain("authorization");
        allowed.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
        other.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();

        static HttpRequestMessage Preflight(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/api/users");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "GET");
            request.Headers.Add("Access-Control-Request-Headers", "authorization");
            return request;
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_Is_Rate_Limited_With_A_Problem_And_Retry_After()
    {
        // Arrange: two attempts per minute
        await using var factory = api.CreateFactory(settings: new Dictionary<string, string?>
        {
            ["RateLimiting:Authentication:PermitLimit"] = "2"
        });
        using var client = factory.CreateClient();
        var attempt = new LoginRequest("nobody@doe.com", "wrong password");

        // Act
        var first = await client.PostAsJsonAsync("/api/auth/login", attempt);
        var second = await client.PostAsJsonAsync("/api/auth/login", attempt);
        var third = await client.PostAsJsonAsync("/api/auth/login", attempt);

        // Assert: the third never reaches the password hasher
        first.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await third.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "Request.TooManyRequests");
        third.Headers.RetryAfter!.Delta.Should().BePositive();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Global_Limit_Counts_Requests_Per_User()
    {
        // Arrange: three requests per minute for everyone
        await using var factory = api.CreateFactory(settings: new Dictionary<string, string?>
        {
            ["RateLimiting:Global:PermitLimit"] = "3"
        });
        using var busy = factory.CreateAuthenticatedClient(role: UserRole.Admin);
        using var other = factory.CreateAuthenticatedClient(role: UserRole.Admin);

        // Act
        for (var i = 0; i < 3; i++)
        {
            (await busy.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var overLimit = await busy.GetAsync("/api/users");
        var otherUser = await other.GetAsync("/api/users");

        // Assert: one user's burst doesn't spend another user's requests
        overLimit.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        otherUser.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed class ThrowingQueries : IUserQueries
    {
        public Task<PagedResult<UserDto>> GetPageAsync(
            int page, int pageSize, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");

        public Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");

        public Task<UserCredentialsDto?> GetCredentialsByEmailAsync(string email, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }
}
