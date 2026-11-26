using System.Net;
using System.Net.Http.Json;
using CqrsApiExample.Contracts.User;
using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsApiExample.Api.IntegrationTests;

/// <summary>Audit fields with EF Core (the interceptor).</summary>
[Collection(ApiCollection.Name)]
public sealed class AuditTests(ApiFixture api) : AuditContract(api.Factory);

/// <summary>The same audit fields with ADO.NET (AdoUnitOfWork).</summary>
[Collection(ApiCollection.Name)]
public sealed class AdoAuditTests(ApiFixture api) : AuditContract(api.AdoFactory);

/// <summary>
///     The user in the access token reaches the audit fields: the request's caller is
///     stored as <c>CreatedBy</c>, and an anonymous registration stores nobody.
/// </summary>
public abstract class AuditContract(ApiFactory factory)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task User_Registered_By_A_Signed_In_Admin_Records_The_Admin()
    {
        // Arrange: registration is anonymous, but a token, when sent, is still read
        var adminId = Guid.NewGuid();
        using var admin = factory.CreateAuthenticatedClient(adminId, UserRole.Admin);

        // Act
        var created = await RegisterAsync(admin);

        // Assert
        (await CreatedByAsync(created.Id)).Should().Be(adminId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Self_Registration_Records_Nobody()
    {
        // Arrange
        using var anonymous = factory.CreateClient();

        // Act
        var created = await RegisterAsync(anonymous);

        // Assert
        (await CreatedByAsync(created.Id)).Should().BeNull();
    }

    private static async Task<UserResponse> RegisterAsync(HttpClient client)
    {
        var request = new CreateUserRequest("John", "Doe", $"user-{Guid.NewGuid():N}@doe.com", "password123");
        var response = await client.PostAsJsonAsync("/api/users", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    private async Task<Guid?> CreatedByAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Users.Where(user => user.Id == userId)
                            .Select(user => user.CreatedBy)
                            .SingleAsync();
    }
}
