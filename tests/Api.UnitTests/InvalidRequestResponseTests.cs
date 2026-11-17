using CqrsApiExample.Api.Common.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsApiExample.Api.UnitTests;

public class InvalidRequestResponseTests
{
    [Fact]
    [Trait("Category", "InvalidRequest")]
    public void Create_Should_Return_ValidationProblem_With_Request_Invalid_Code()
    {
        // Arrange: what MVC produces for a body it cannot parse
        var services = new ServiceCollection();
        services.AddOptions<ApiBehaviorOptions>();
        services.AddSingleton<ProblemDetailsFactory, CqrsApiExampleProblemDetailsFactory>();
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        actionContext.ModelState.AddModelError("$.name", "Expected depth to be zero at the end of the JSON payload.");

        // Act
        var result = InvalidRequestResponse.Create(actionContext);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.ContentTypes.Should().Contain("application/problem+json");
        var problem = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().ContainKey("$.name");
        problem.Extensions["errorCodes"].Should().BeAssignableTo<IEnumerable<string>>()
               .Which.Should().Equal(RequestErrors.Invalid.Code);
    }
}
