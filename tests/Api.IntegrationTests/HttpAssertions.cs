using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace CqrsApiExample.Api.IntegrationTests;

internal static class HttpAssertions
{
    /// <summary>
    ///     Asserts the status code, that the body is ProblemDetails
    ///     (<c>application/problem+json</c>) and returns it parsed.
    /// </summary>
    public static async Task<JsonElement> ShouldBeProblemAsync(this HttpResponseMessage response,
                                                               HttpStatusCode expectedStatus,
                                                               params string[] expectedErrorCodes)
    {
        response.StatusCode.Should().Be(expectedStatus);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be((int)expectedStatus);
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
        problem.GetProperty("errorCodes").EnumerateArray().Select(code => code.GetString())
               .Should().Equal(expectedErrorCodes);

        return problem;
    }
}
