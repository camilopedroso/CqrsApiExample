using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CqrsApiExample.Contracts.User;
using FluentAssertions;

namespace CqrsApiExample.Api.IntegrationTests;

/// <summary>The HTTP contract with the default persistence provider (EF Core).</summary>
[Collection(ApiCollection.Name)]
public sealed class UsersEndpointsTests(ApiFixture api) : UsersEndpointsContract(api.Factory);

/// <summary>
///     The same HTTP contract with <c>Persistence:Provider=Ado</c>: switching the
///     provider must not change a single status code, error code or side effect.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AdoUsersEndpointsTests(ApiFixture api) : UsersEndpointsContract(api.AdoFactory);

/// <summary>
///     Locks the HTTP contract end to end: status codes, content types,
///     error codes and side effects. Tests share one API and database, so each
///     one uses its own email addresses. Runs once per persistence provider.
/// </summary>
public abstract class UsersEndpointsContract(ApiFactory factory)
{
    private readonly ApiFactory _factory = factory;
    // Sends a valid token: reading users requires one. Registering (POST) doesn't, so it works either way.
    private readonly HttpClient _client = factory.CreateAuthenticatedClient();

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetUsers_Returns_Seeded_User_As_Json()
    {
        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        users.Should().Contain(user => user.Email == "jane.doe@example.com");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_Returns_201_With_Location_Normalizes_Data_And_Sends_Welcome_Email()
    {
        // Arrange
        var email = NewEmail();

        // Act
        var response = await _client.PostAsJsonAsync("/api/users",
                                                      new CreateUserRequest(" John ", "Doe", email.ToUpperInvariant(), "password123"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<UserResponse>();
        created!.Name.Should().Be("John");
        created.Email.Should().Be(email);
        response.Headers.Location!.AbsolutePath.Should().BeEquivalentTo($"/api/Users/{created.Id}");

        var fetched = await _client.GetFromJsonAsync<UserResponse>(response.Headers.Location);
        fetched.Should().Be(created);

        // Sent by the outbox in the background, shortly after the response.
        await Eventually.AssertAsync(() =>
            _factory.Emails.Sent.Should().ContainSingle(sent => sent.To == email)
                    .Which.Subject.Should().Be("Welcome!"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Welcome_Email_Is_Retried_Until_The_Email_Service_Works()
    {
        // Arrange: the email service fails twice for this address
        var email = NewEmail();
        _factory.Emails.FailFor(email, times: 2);

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", new CreateUserRequest("John", "Doe", email, "password123"));

        // Assert: the user is created right away; the email arrives after the retries
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        await Eventually.AssertAsync(() =>
            _factory.Emails.Sent.Should().ContainSingle(sent => sent.To == email));
        _factory.Emails.FailedAttempts(email).Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_Returns_409_For_Duplicate_Email_Ignoring_Case_Without_Second_Email()
    {
        // Arrange
        var email = NewEmail();
        (await _client.PostAsJsonAsync("/api/users", new CreateUserRequest("John", "Doe", email, "password123")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var response = await _client.PostAsJsonAsync("/api/users",
                                                      new CreateUserRequest("Johnny", "Doe", email.ToUpperInvariant(), "password123"));

        // Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "User.DuplicateEmail");
        problem.GetProperty("type").GetString().Should().Be("User.DuplicateEmail");
        await ExactlyOneEmailAsync(email);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_Concurrent_Requests_With_Same_Email_Create_Exactly_One_User()
    {
        // Arrange
        var email = NewEmail();

        // Act
        var responses = await Task.WhenAll(Enumerable.Range(1, 10).Select(i =>
            _client.PostAsJsonAsync("/api/users", new CreateUserRequest($"Racer{i}", "Doe", email, "password123"))));

        // Assert: the unique index turns the losers into 409s, never 500s
        responses.Select(response => response.StatusCode).Should()
                 .ContainSingle(status => status == HttpStatusCode.Created)
                 .And.OnlyContain(status => status == HttpStatusCode.Created || status == HttpStatusCode.Conflict);

        var users = await _client.GetFromJsonAsync<List<UserResponse>>("/api/users");
        users!.Count(user => user.Email == email).Should().Be(1);
        await ExactlyOneEmailAsync(email);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_Returns_400_Grouped_By_Field_With_Error_Codes()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/users", new CreateUserRequest("", "Doe", "not-an-email", "password123"));

        // Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest,
                                                          "User.InvalidName", "User.InvalidEmail");
        var errors = problem.GetProperty("errors");
        errors.GetProperty("Name")[0].GetString().Should().Be("'Name' must not be empty.");
        errors.GetProperty("Email")[0].GetString().Should().Be("'Email' is not a valid email address.");
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("""{ "name": "John", "email": "missing@doe.com", "password": "password123" }""", "User.InvalidSurname")]
    [InlineData("""{ "name": "John", "surname": null, "email": "null@doe.com", "password": "password123" }""", "User.InvalidSurname")]
    [InlineData("""{ "name": """, "Request.Invalid")]
    public async Task CreateUser_Returns_400_For_Missing_Null_Or_Unreadable_Input(string body, string expectedCode)
    {
        // Act
        var response = await _client.PostAsync("/api/users",
                                               new StringContent(body, Encoding.UTF8, "application/json"));

        // Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, expectedCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetUser_Returns_404_When_User_Does_Not_Exist()
    {
        // Act
        var response = await _client.GetAsync($"/api/users/{Guid.NewGuid()}");

        // Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "User.NotFound");
        problem.GetProperty("title").GetString().Should().Be("User not found.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetUser_Returns_400_For_Empty_Guid()
    {
        // Act
        var response = await _client.GetAsync($"/api/users/{Guid.Empty}");

        // Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "User.InvalidId");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetUser_Returns_404_Request_NotFound_When_Id_Is_Not_A_Guid()
    {
        // Act: the route only matches GUIDs, so no endpoint is found
        var response = await _client.GetAsync("/api/users/not-a-guid");

        // Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Request.NotFound");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Unsupported_Method_Returns_405_Problem_With_Allow_Header()
    {
        // Act
        var response = await _client.DeleteAsync($"/api/users/{Guid.NewGuid()}");

        // Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.MethodNotAllowed, "Request.MethodNotAllowed");
        response.Content.Headers.Allow.Should().Equal("GET");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Unsupported_Content_Type_Returns_415_Problem()
    {
        // Act
        var response = await _client.PostAsync("/api/users",
                                               new StringContent("Ada Lovelace", Encoding.UTF8, "text/plain"));

        // Assert
        await response.ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, "Request.UnsupportedMediaType");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Unreadable_Json_Does_Not_Leak_Internal_Type_Names()
    {
        // Act: a number where a string is expected
        var response = await _client.PostAsync("/api/users",
                                               new StringContent("""{ "name": 123, "surname": "Doe", "email": "a@doe.com" }""",
                                                                 Encoding.UTF8,
                                                                 "application/json"));

        // Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Request.Invalid");
        problem.GetProperty("errors").GetProperty("$.name")[0].GetString().Should().Be("The input was not valid.");
        problem.GetRawText().Should().NotContain("CqrsApiExample.Contracts");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task OpenApi_Document_Describes_The_Users_Endpoints()
    {
        // Act
        var document = await _client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        // Assert
        var createResponses = document.GetProperty("paths").GetProperty("/api/Users")
                                      .GetProperty("post").GetProperty("responses");
        createResponses.EnumerateObject().Select(response => response.Name)
                       .Should().BeEquivalentTo(["201", "400", "409", "500"]);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@doe.com";

    /// <summary>The first email arrives, and no second one follows during several outbox polls.</summary>
    private async Task ExactlyOneEmailAsync(string email)
    {
        await Eventually.AssertAsync(() => _factory.Emails.Sent.Count(sent => sent.To == email).Should().Be(1));
        await Eventually.StaysTrueAsync(() => _factory.Emails.Sent.Count(sent => sent.To == email).Should().Be(1));
    }
}
