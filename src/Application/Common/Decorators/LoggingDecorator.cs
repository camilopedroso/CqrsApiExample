using System.Diagnostics;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Domain.Common.Results;
using Microsoft.Extensions.Logging;

namespace CqrsApiExample.Application.Common.Decorators;

/// <summary>
///     Logs how long each request took and, when it fails, its error codes.
///     The request itself is not logged because it may carry personal data.
///     Exceptions are left to the global exception handler, so they are not
///     logged twice.
/// </summary>
/// <remarks>
///     <para>
///         This is the outermost decorator: <c>AddRequestHandlers</c> builds the chain
///         <c>LoggingDecorator → ValidationDecorator → handler</c>, so the elapsed time
///         includes validation, and a request rejected by its validator is logged here
///         as a failure with its validation codes.
///     </para>
///     <para>
///         The constraint <c>TResult : IFailureResult&lt;TResult&gt;</c> is what lets this
///         generic class read <see cref="IFailureResult{TSelf}.IsError" /> and
///         <see cref="IFailureResult{TSelf}.Errors" /> without knowing the value type of
///         the <c>Result&lt;T&gt;</c>.
///     </para>
/// </remarks>
public sealed class LoggingDecorator<TRequest, TResult>(
    IRequestHandler<TRequest, TResult> inner,
    ILogger<LoggingDecorator<TRequest, TResult>> logger) : IRequestHandler<TRequest, TResult>
                                                                where TRequest : IRequest<TResult>
                                                                where TResult : IFailureResult<TResult>
{
    public async Task<TResult> Handle(TRequest request, CancellationToken cancellationToken)
    {
        // Only the type name (e.g. "CreateUserCommand") is logged, never the request's values.
        string requestName = typeof(TRequest).Name;

        // A raw timestamp instead of a Stopwatch instance: nothing is allocated per request.
        long startedAt = Stopwatch.GetTimestamp();

        // No try/catch: an exception goes straight up to GlobalExceptionHandler, which logs it
        // once with the stack trace. Catching it here would log the same failure twice.
        TResult result = await inner.Handle(request, cancellationToken);

        double elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        if (result.IsError)
        {
            // An expected failure (validation, not found, conflict) is a Warning, not an Error:
            // the API answered correctly with a 4xx. Only the codes (Area.Name) are logged,
            // because the descriptions can echo what the client sent.
            string[] errorCodes = [.. result.Errors.Select(error => error.Code)];

            // Structured logging: each {Placeholder} becomes a named property of the log entry,
            // and the array is written as a comma-separated list.
            logger.LogWarning(
                "{RequestName} failed in {ElapsedMs:0.0} ms with {ErrorCodes}",
                requestName,
                elapsedMs,
                errorCodes);
        }
        else
        {
            logger.LogInformation(
                "{RequestName} handled in {ElapsedMs:0.0} ms",
                requestName,
                elapsedMs);
        }

        // The result passes through untouched: this decorator only observes.
        return result;
    }
}
