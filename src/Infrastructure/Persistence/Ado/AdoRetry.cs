namespace CqrsApiExample.Infrastructure.Persistence.Ado;

/// <summary>
///     The ADO.NET counterpart of EF Core's <c>EnableRetryOnFailure</c> (see
///     <c>AddPersistence</c>), with the same limits: up to 3 retries of
///     transient failures, at most 5 s apart. The whole unit (connection,
///     transaction, commands) is retried, never a single command inside a
///     transaction. Without it the comparison between providers would not be
///     fair.
/// </summary>
internal static class AdoRetry
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(5);

    public static async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation,
                                                CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (attempt < MaxRetries
                                       && !cancellationToken.IsCancellationRequested
                                       && SqlServerErrors.IsTransient(ex))
            {
                await Task.Delay(DelayFor(attempt), cancellationToken);
            }
        }
    }

    public static Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken) =>
        ExecuteAsync(async token =>
        {
            await operation(token);
            return true;
        }, cancellationToken);

    // 1 s, 2 s, 4 s, ... capped at 5 s.
    private static TimeSpan DelayFor(int attempt) =>
        TimeSpan.FromTicks(Math.Min(BaseDelay.Ticks << attempt, MaxDelay.Ticks));
}
