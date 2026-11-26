using System.Net;
using CqrsApiExample.Application.Auth;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Common.Paging;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Domain.UserAggregate;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsApiExample.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class UnexpectedErrorTests(ApiFixture api)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Unhandled_Exception_Returns_500_ProblemDetails_With_Detail_In_Development()
    {
        // Arrange
        await using var factory = api.CreateFactory(services => services.AddScoped<IUserQueries, FailingUserQueries>());
        using var client = factory.CreateAuthenticatedClient(role: UserRole.Admin);

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.InternalServerError, "General.Unexpected");
        problem.GetProperty("type").GetString().Should().Be("General.Unexpected");
        problem.GetProperty("detail").GetString().Should().Be(FailingUserQueries.Message);
    }

    private sealed class FailingUserQueries : IUserQueries
    {
        public const string Message = "The read database is unavailable.";

        public Task<PagedResult<UserDto>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Message);

        public Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Message);

        public Task<UserCredentialsDto?> GetCredentialsByEmailAsync(string email, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Message);
    }
}
