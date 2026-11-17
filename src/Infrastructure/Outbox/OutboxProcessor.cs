using CqrsApiExample.Application.Common.Events;
using CqrsApiExample.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CqrsApiExample.Infrastructure.Outbox;

/// <summary>
///     Handles one batch of pending outbox messages: claims them, runs every
///     handler that hasn't handled each message yet, and schedules a retry with
///     exponential backoff when something fails. Delivery is at-least-once.
/// </summary>
internal sealed class OutboxProcessor(ApplicationDbContext context,
                                      IDomainEventDispatcher dispatcher,
                                      TimeProvider timeProvider,
                                      IOptions<OutboxOptions> options,
                                      ILogger<OutboxProcessor> logger)
{
    private readonly OutboxOptions _options = options.Value;

    /// <returns>How many messages were claimed.</returns>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var messages = await ClaimBatchAsync(cancellationToken);

        foreach (var message in messages)
        {
            await ProcessAsync(message, cancellationToken);
        }

        return messages.Count;
    }

    /// <summary>
    ///     Leases due messages in a single statement. READPAST skips rows another
    ///     processor is claiming right now, so instances never share a message and
    ///     no transaction stays open while handlers run. The table name is written
    ///     with its schema (<see cref="Schemas.Messaging" />) because an
    ///     interpolated value would become a parameter, not an identifier.
    /// </summary>
    private Task<List<OutboxMessage>> ClaimBatchAsync(CancellationToken cancellationToken)
    {
        var now = Now();
        var lockedUntil = now + _options.LockDuration;

        return context.OutboxMessages
                      .FromSql($"""
                                WITH due AS (
                                    SELECT TOP ({_options.BatchSize}) *
                                    FROM [Messaging].[OutboxMessages] WITH (ROWLOCK, UPDLOCK, READPAST)
                                    WHERE ProcessedAt IS NULL
                                      AND Attempts < {_options.MaxAttempts}
                                      AND NextAttemptAt <= {now}
                                      AND (LockedUntil IS NULL OR LockedUntil < {now})
                                    ORDER BY OccurredAt)
                                UPDATE due SET LockedUntil = {lockedUntil}
                                OUTPUT INSERTED.*
                                """)
                      .ToListAsync(cancellationToken);
    }

    private async Task ProcessAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var domainEvent = OutboxSerializer.Deserialize(message.Type, message.Content);
            var completedHandlers = await context.OutboxMessageConsumers
                                                 .Where(consumer => consumer.MessageId == message.Id)
                                                 .Select(consumer => consumer.HandlerName)
                                                 .ToListAsync(cancellationToken);

            foreach (var invocation in dispatcher.GetInvocations(domainEvent))
            {
                if (completedHandlers.Contains(invocation.HandlerName))
                {
                    continue;
                }

                await invocation.InvokeAsync(cancellationToken);

                // Saved right away, so a failure in a later handler doesn't make this one run again.
                context.OutboxMessageConsumers.Add(new OutboxMessageConsumer(message.Id, invocation.HandlerName, Now()));
                await context.SaveChangesAsync(cancellationToken);
            }

            message.ProcessedAt = Now();
            message.Error = null;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            ScheduleRetry(message, ex);
        }

        // On shutdown the message is not touched: its lease expires and it is picked up again.
        message.LockedUntil = null;
        await context.SaveChangesAsync(cancellationToken);
    }

    private void ScheduleRetry(OutboxMessage message, Exception exception)
    {
        message.Attempts++;
        message.Error = exception.ToString();

        var delay = TimeSpan.FromTicks(Math.Min(_options.BaseRetryDelay.Ticks * (1L << Math.Min(message.Attempts - 1, 30)),
                                                _options.MaxRetryDelay.Ticks));
        message.NextAttemptAt = Now() + delay;

        if (message.Attempts >= _options.MaxAttempts)
        {
            logger.LogError(exception,
                            "Outbox message {MessageId} ({MessageType}) failed {Attempts} times and will not be retried",
                            message.Id, message.Type, message.Attempts);
        }
        else
        {
            logger.LogWarning(exception,
                              "Outbox message {MessageId} ({MessageType}) failed (attempt {Attempts}); next attempt at {NextAttemptAt:O}",
                              message.Id, message.Type, message.Attempts, message.NextAttemptAt);
        }
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
