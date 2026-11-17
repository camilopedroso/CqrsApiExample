using CqrsApiExample.Api.Common.Errors;
using CqrsApiExample.Domain.Common.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace CqrsApiExample.Api;

/// <summary>
///     Registers the presentation layer (ASP.NET Core): controllers, the shape of every
///     error response (ProblemDetails with <c>errorCodes</c>) and the OpenAPI document.
/// </summary>
/// <remarks>
///     Called first in <c>Program.cs</c>:
///     <c>AddPresentation().AddApplication().AddInfrastructure().AddPersistence(config)</c>.
///     The middleware that uses these services (<c>UseExceptionHandler</c>,
///     <c>UseStatusCodePages</c>, <c>MapOpenApi</c>) is configured in <c>Program.cs</c>.
/// </remarks>
public static class DependencyInjection
{
    // ProblemDetails extension that carries the error codes on every problem response.
    private const string ErrorCodesKey = "errorCodes";

    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        AddControllers(services);
        AddErrorResponses(services);

        // The OpenAPI document; Program.cs maps it (/openapi/v1.json) and the Swagger UI in Development.
        services.AddOpenApi();

        return services;
    }

    /// <summary>
    ///     Controllers, configured so that bad input always reaches the client in the same
    ///     ValidationProblemDetails shape, with error codes.
    /// </summary>
    private static void AddControllers(IServiceCollection services)
    {
        services.AddControllers(ConfigureMvc)
                .ConfigureApiBehaviorOptions(ConfigureApiBehavior)
                .AddJsonOptions(ConfigureJson);

        // Missing/null fields must reach the FluentValidation pipeline, which answers with the
        // domain error codes; otherwise MVC rejects non-nullable strings first, in another format.
        static void ConfigureMvc(MvcOptions options) =>
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

        // Input MVC cannot bind at all (e.g. malformed JSON) is answered by InvalidRequestResponse,
        // in the same shape as the validation errors and with the code Request.Invalid.
        static void ConfigureApiBehavior(ApiBehaviorOptions options) =>
            options.InvalidModelStateResponseFactory = InvalidRequestResponse.Create;

        // JSON parser messages can name internal types ("could not be converted to
        // CqrsApiExample.Contracts..."); clients get a generic message under the JSON path instead.
        static void ConfigureJson(JsonOptions options) =>
            options.AllowInputFormatterExceptionMessages = false;
    }

    /// <summary>
    ///     Every error response is a ProblemDetails with <c>errorCodes</c>, whoever writes it:
    ///     a controller, the framework or the global exception handler.
    /// </summary>
    private static void AddErrorResponses(IServiceCollection services)
    {
        // Problems written by MVC (controllers, 415...): adds errorCodes and traceId.
        services.AddSingleton<ProblemDetailsFactory, CqrsApiExampleProblemDetailsFactory>();

        // Problems written outside MVC, by IProblemDetailsService (UseStatusCodePages, the exception handler).
        services.AddProblemDetails(options => options.CustomizeProblemDetails = AddRequestErrorCode);

        // Unexpected exceptions: logged once, answered with a 500 (General.Unexpected) or a 499 if cancelled.
        services.AddExceptionHandler<GlobalExceptionHandler>();
    }

    /// <summary>
    ///     Problems written outside MVC (e.g. by UseStatusCodePages for an unknown route or a
    ///     wrong method) get the matching request-level code, like every other problem.
    /// </summary>
    private static void AddRequestErrorCode(ProblemDetailsContext context)
    {
        ProblemDetails problem = context.ProblemDetails;

        // Application errors (User.*) were already added; they take precedence.
        if (problem.Extensions.ContainsKey(ErrorCodesKey))
        {
            return;
        }

        // An empty routing response has no Status yet, so the response's status code is used.
        int statusCode = problem.Status ?? context.HttpContext.Response.StatusCode;
        Error? error = RequestErrors.ForStatusCode(statusCode);

        // Only 404, 405 and 415 have a request-level error; any other status keeps the problem as it is.
        if (error is null)
        {
            return;
        }

        string[] errorCodes = [error.Code];
        problem.Extensions[ErrorCodesKey] = errorCodes;
    }
}
