using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CqrsApiExample.Contracts.Auth;
using CqrsApiExample.Contracts.Common;
using CqrsApiExample.Contracts.User;
using CqrsApiExample.Infrastructure.Data;
using FluentAssertions;

namespace CqrsApiExample.Api.IntegrationTests;

/// <summary>
///     The whole API on a database login that may only read and write rows (as in Development
///     and Docker Compose): every feature, the outbox included, works without DDL rights.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class LeastPrivilegeTests(ApiFixture api)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Api_Works_End_To_End_On_The_Least_Privilege_Login()
    {
        // Arrange: startup migrates and seeds as sa, then creates the application login
        await using var factory = api.CreateLeastPrivilegeFactory();
        using var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@doe.com";

        // Act: registration (insert + outbox row), login (read), listing as the seeded admin
        var registration = await client.PostAsJsonAsync("/api/users", new CreateUserRequest("John", "Doe", email, "password123"));
        var login = await client.PostAsJsonAsync("/api/auth/login",
                                                 new LoginRequest("jane.doe@example.com", DbInitializer.SeedUserPassword));
        var token = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/users?pageSize=100");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        var list = await client.SendAsync(listRequest);

        // Assert
        registration.StatusCode.Should().Be(HttpStatusCode.Created);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await list.Content.ReadFromJsonAsync<PagedResponse<UserResponse>>())!.Items
            .Should().Contain(user => user.Email == email);

        // The outbox processor (UPDATE ... OUTPUT on Messaging) also runs on that login.
        await Eventually.AssertAsync(() => factory.Emails.Sent.Should().Contain(sent => sent.To == email));
    }
}
