using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CqrsApiExample.Infrastructure.Outbox;

/// <summary>Runs <see cref="OutboxProcessor"/> in a loop for as long as the application runs.</summary>
internal sealed class OutboxBackgroundService(IServiceScopeFactory scopeFactory,
                                              IOptions<OutboxOptions> options,
                                              TimeProvider timeProvider,
                                              ILogger<OutboxBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var outboxOptions = options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = 0;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                claimed = await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // E.g. the database is down. Keep the loop alive and try again later.
                logger.LogError(ex, "Outbox processing failed; trying again in {PollingInterval}", outboxOptions.PollingInterval);
            }

            // A full batch means more messages may be waiting, so go again right away.
            if (claimed < outboxOptions.BatchSize)
            {
                try
                {
                    await Task.Delay(outboxOptions.PollingInterval, timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
