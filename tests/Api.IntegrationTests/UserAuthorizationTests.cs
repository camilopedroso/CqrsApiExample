using System.Net;
using System.Net.Http.Json;
using CqrsApiExample.Contracts.User;
using CqrsApiExample.Domain.UserAggregate;
using FluentAssertions;

namespace CqrsApiExample.Api.IntegrationTests;

/// <summary>
///     Who may read which user: members only themselves, administrators anyone. The rules
///     live in the Application (AuthorizationDecorator); these tests prove they reach HTTP.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class UserAuthorizationTests(ApiFixture api)
{
    private readonly ApiFactory _factory = api.Factory;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Member_Cannot_List_Users()
    {
        // Arrange
        using var member = _factory.CreateAuthenticatedClient(role: UserRole.Member);

        // Act
        var response = await member.GetAsync("/api/users");

        // Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Member_Can_Read_Itself_But_Not_Another_User()
    {
        // Arrange
        var me = await RegisterAsync();
        var someoneElse = await RegisterAsync();
        using var member = _factory.CreateAuthenticatedClient(me.Id, UserRole.Member);

        // Act
        var own = await member.GetAsync($"/api/users/{me.Id}");
        var other = await member.GetAsync($"/api/users/{someoneElse.Id}");

        // Assert
        own.StatusCode.Should().Be(HttpStatusCode.OK);
        (await own.Content.ReadFromJsonAsync<UserResponse>()).Should().Be(me);
        await other.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Member_Gets_403_Not_404_For_An_Id_That_Does_Not_Exist()
    {
        // Arrange
        using var member = _factory.CreateAuthenticatedClient(role: UserRole.Member);

        // Act: a 404 here would confirm which ids exist
        var missing = await member.GetAsync($"/api/users/{Guid.NewGuid()}");
        var empty = await member.GetAsync($"/api/users/{Guid.Empty}");

        // Assert: authorization runs before validation and the handler
        await missing.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
        await empty.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Admin_Can_Read_Anyone_And_Gets_404_For_A_Missing_User()
    {
        // Arrange
        var someone = await RegisterAsync();
        using var admin = _factory.CreateAuthenticatedClient(role: UserRole.Admin);

        // Act
        var existing = await admin.GetAsync($"/api/users/{someone.Id}");
        var missing = await admin.GetAsync($"/api/users/{Guid.NewGuid()}");

        // Assert
        existing.StatusCode.Should().Be(HttpStatusCode.OK);
        await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "User.NotFound");
    }

    private async Task<UserResponse> RegisterAsync()
    {
        using var anonymous = _factory.CreateClient();
        var request = new CreateUserRequest("John", "Doe", $"user-{Guid.NewGuid():N}@doe.com", "password123");
        var response = await anonymous.PostAsJsonAsync("/api/users", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }
}
