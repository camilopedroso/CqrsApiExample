namespace CqrsApiExample.Api.IntegrationTests;

/// <summary>
///     For effects that happen in the background (the outbox): retries an
///     assertion until it passes or the timeout expires.
/// </summary>
internal static class Eventually
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    public static async Task AssertAsync(Action assertion, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        while (true)
        {
            try
            {
                assertion();
                return;
            }
            catch when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(Interval);
            }
        }
    }

    /// <summary>
    ///     Asserts that something stays true for a while, e.g. that no second
    ///     email arrives after the first one.
    /// </summary>
    public static async Task StaysTrueAsync(Action assertion, TimeSpan? duration = null)
    {
        var until = DateTime.UtcNow + (duration ?? TimeSpan.FromSeconds(1));
        while (DateTime.UtcNow < until)
        {
            assertion();
            await Task.Delay(Interval);
        }
    }
}
