using System.Security.Claims;
using CqrsApiExample.Api.Common.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace CqrsApiExample.Api.UnitTests;

public class HttpContextCurrentUserTests
{
    [Fact]
    [Trait("Category", "Authentication")]
    public void Reads_Id_And_Role_From_The_Token_Claims()
    {
        // Arrange
        var id = Guid.NewGuid();
        var currentUser = CurrentUserWith(authenticated: true, ("sub", id.ToString()), ("role", "Admin"));

        // Assert
        currentUser.IsAuthenticated.Should().BeTrue();
        currentUser.Id.Should().Be(id);
        currentUser.IsInRole("Admin").Should().BeTrue();
        currentUser.IsInRole("Member").Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void Is_Anonymous_Without_An_Authenticated_Identity_Even_If_Claims_Are_There()
    {
        // Arrange: claims that were never validated must not count
        var currentUser = CurrentUserWith(authenticated: false, ("sub", Guid.NewGuid().ToString()), ("role", "Admin"));

        // Assert
        currentUser.IsAuthenticated.Should().BeFalse();
        currentUser.Id.Should().BeNull();
        currentUser.IsInRole("Admin").Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void Is_Anonymous_Outside_A_Request()
    {
        // Arrange: e.g. the outbox background service
        var currentUser = new HttpContextCurrentUser(new HttpContextAccessor());

        // Assert
        currentUser.IsAuthenticated.Should().BeFalse();
        currentUser.Id.Should().BeNull();
    }

    private static HttpContextCurrentUser CurrentUserWith(bool authenticated, params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(claim => new Claim(claim.Type, claim.Value)),
                                          authenticationType: authenticated ? "Bearer" : null,
                                          nameType: "sub",
                                          roleType: "role");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = context });
    }
}
