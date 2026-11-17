namespace CqrsApiExample.Infrastructure.Outbox;

/// <summary>
///     A domain event waiting to be handled. It is saved in the same transaction
///     as the change that raised it, so it exists if and only if that change was
///     committed.
/// </summary>
internal sealed class OutboxMessage
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>Full name of the domain event type (see <see cref="OutboxSerializer"/>).</summary>
    public required string Type { get; init; }

    /// <summary>The domain event as JSON.</summary>
    public required string Content { get; init; }

    public DateTime OccurredAt { get; init; }

    /// <summary>Not picked up before this moment (backoff after a failure).</summary>
    public DateTime NextAttemptAt { get; set; }

    /// <summary>Lease: a processor owns the message until then.</summary>
    public DateTime? LockedUntil { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    /// <summary>Last failure, kept for diagnosis.</summary>
    public string? Error { get; set; }
}

/// <summary>Records that a handler finished a message, so a retry never runs it again.</summary>
internal sealed record OutboxMessageConsumer(Guid MessageId, string HandlerName, DateTime ProcessedAt);
