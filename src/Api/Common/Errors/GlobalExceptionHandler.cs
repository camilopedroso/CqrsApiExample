using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DomainErrors = CqrsApiExample.Domain.Common.DomainErrors.Errors;

namespace CqrsApiExample.Api.Common.Errors;

/// <summary>
///     Last line of defence for exceptions nobody expected: logs them once,
///     with the stack trace, and answers with a ProblemDetails body that
///     doesn't leak internals outside Development.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    // Non-standard, but widely used for "the client went away before we answered".
    public const int StatusClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request {Method} {Path} was cancelled by the client",
                                  httpContext.Request.Method,
                                  httpContext.Request.Path);
            httpContext.Response.StatusCode = StatusClientClosedRequest;
            return true;
        }

        logger.LogError(exception,
                        "Unhandled exception while processing {Method} {Path}",
                        httpContext.Request.Method,
                        httpContext.Request.Path);

        var error = DomainErrors.Generic.Ops;
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = error.Description,
                Type = error.Code,
                Detail = environment.IsDevelopment() ? exception.Message : null,
                Extensions = { ["errorCodes"] = new[] { error.Code } }
            }
        });
    }
}
