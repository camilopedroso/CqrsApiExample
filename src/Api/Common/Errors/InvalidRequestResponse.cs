using CqrsApiExample.Api.Common.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace CqrsApiExample.Api.Common.Errors;

/// <summary>
///     Response for requests MVC cannot bind (e.g. malformed JSON). Business
///     validation happens in the application pipeline; this only covers input
///     that never reaches it, with the same shape and an error code.
/// </summary>
public static class InvalidRequestResponse
{
    public static IActionResult Create(ActionContext context)
    {
        context.HttpContext.Items[HttpContextItemKeys.Errors] = new[] { RequestErrors.Invalid };

        ProblemDetailsFactory problemDetailsFactory =
            context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();

        ValidationProblemDetails problemDetails =
            problemDetailsFactory.CreateValidationProblemDetails(context.HttpContext,
                                                                 context.ModelState);

        return new BadRequestObjectResult(problemDetails)
        {
            ContentTypes = { "application/problem+json" }
        };
    }
}
