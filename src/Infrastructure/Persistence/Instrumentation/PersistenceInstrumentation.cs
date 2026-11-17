using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CqrsApiExample.Infrastructure.Persistence.Instrumentation;

/// <summary>
///     Times every persistence call, whichever provider serves it, so EF Core and
///     ADO.NET are measured the same way. Each call is logged
///     (<c>Persistence Ado UserQueries.GetAll succeeded in 1.234 ms</c>) and
///     recorded in the <see cref="DurationInstrumentName"/> histogram of the
///     <see cref="MeterName"/> meter, tagged with provider, operation and outcome
///     (watch it live with <c>dotnet-counters monitor -n CqrsApiExample.Api --counters CqrsApiExample.Persistence</c>).
///     Exceptions are not logged here: the global exception handler does that once.
/// </summary>
internal sealed class PersistenceInstrumentation(IOptions<PersistenceOptions> options,
                                                 ILogger<PersistenceInstrumentation> logger)
{
    public const string MeterName = "CqrsApiExample.Persistence";
    public const string DurationInstrumentName = "cqrsapi.persistence.operation.duration";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>(DurationInstrumentName,
                                      unit: "ms",
                                      description: "Duration of persistence operations, by provider.");

    private readonly string _provider = options.Value.Provider.ToString();

    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = Outcome.Failed;
        try
        {
            var result = await action();
            outcome = Outcome.Succeeded;
            return result;
        }
        catch (OperationCanceledException)
        {
            outcome = Outcome.Cancelled;
            throw;
        }
        finally
        {
            Record(operation, outcome, Stopwatch.GetElapsedTime(startedAt));
        }
    }

    public Task MeasureAsync(string operation, Func<Task> action) =>
        MeasureAsync(operation, async () =>
        {
            await action();
            return true;
        });

    public void Measure(string operation, Action action)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = Outcome.Failed;
        try
        {
            action();
            outcome = Outcome.Succeeded;
        }
        finally
        {
            Record(operation, outcome, Stopwatch.GetElapsedTime(startedAt));
        }
    }

    private void Record(string operation, string outcome, TimeSpan elapsed)
    {
        var elapsedMs = elapsed.TotalMilliseconds;

        Duration.Record(elapsedMs,
                        new KeyValuePair<string, object?>("provider", _provider),
                        new KeyValuePair<string, object?>("operation", operation),
                        new KeyValuePair<string, object?>("outcome", outcome));

        logger.LogInformation("Persistence {Provider} {Operation} {Outcome} in {ElapsedMs:0.000} ms",
                              _provider,
                              operation,
                              outcome,
                              elapsedMs);
    }

    private static class Outcome
    {
        public const string Succeeded = "succeeded";
        public const string Failed = "failed";
        public const string Cancelled = "cancelled";
    }
}
