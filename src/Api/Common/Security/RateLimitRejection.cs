using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CqrsApiExample.Api.Common.Security;

/// <summary>What a client over its limit gets: 429 and when to try again.</summary>
/// <remarks>
///     The body is left empty on purpose: <c>UseStatusCodePages</c> turns the empty 429 into a
///     ProblemDetails with <c>Request.TooManyRequests</c>, like every other framework error.
/// </remarks>
internal static class RateLimitRejection
{
    public static ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        // The fixed window limiter knows when its window resets; Retry-After is in whole seconds.
        bool hasRetryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter);

        if (hasRetryAfter)
        {
            int seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    }
}
