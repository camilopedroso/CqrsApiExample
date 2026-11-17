namespace CqrsApiExample.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    /// <summary>Wait between polls when there was nothing (or less than a full batch) to process.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 20;

    /// <summary>After this many failures a message is left with its error (dead letter).</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Delay after the first failure; it doubles on every new failure, up to <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    ///     How long a claimed message stays reserved for one processor. If the
    ///     process dies, another one picks the message up after this.
    /// </summary>
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(1);
}
