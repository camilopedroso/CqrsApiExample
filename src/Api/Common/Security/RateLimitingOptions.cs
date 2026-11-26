using System.Threading.RateLimiting;

namespace CqrsApiExample.Api.Common.Security;

/// <summary>
///     The <c>RateLimiting</c> configuration section: how many requests a client may make per window,
///     overall and on the endpoints that are worth guessing at (login and registration).
/// </summary>
/// <remarks>
///     A client is the signed-in user (the token's <c>sub</c>) when there is one, otherwise its IP
///     address. Behind a reverse proxy every request would come from the proxy's address, so
///     forwarded headers must be configured first (<c>UseForwardedHeaders</c>); this example has no proxy.
/// </remarks>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Every request, per client: generous, it only stops runaway clients.</summary>
    public FixedWindowSettings Global { get; set; } = new() { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) };

    /// <summary>
    ///     Login and registration, per IP address: strict, because each attempt runs the slow password
    ///     hasher and a burst of them is how passwords get guessed.
    /// </summary>
    public FixedWindowSettings Authentication { get; set; } = new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) };

    /// <summary>At most <see cref="PermitLimit"/> requests in each <see cref="Window"/>; the rest get 429.</summary>
    public sealed class FixedWindowSettings
    {
        public int PermitLimit { get; set; }

        public TimeSpan Window { get; set; }

        /// <summary>
        ///     The limiter's own options. No queue: a request over the limit is refused at once (with
        ///     <c>Retry-After</c>) instead of being held open, which would cost the server more.
        /// </summary>
        public FixedWindowRateLimiterOptions ToLimiterOptions()
        {
            FixedWindowRateLimiterOptions options = new()
            {
                PermitLimit = PermitLimit,
                Window = Window,
                QueueLimit = 0,
                AutoReplenishment = true
            };

            return options;
        }
    }
}
