using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Infrastructure.Persistence.Instrumentation;

namespace CqrsApiExample.Infrastructure.Persistence.Decorators;

/// <summary>
///     Times every <see cref="IUnitOfWork"/> commit of whichever provider is configured
///     (EF Core or ADO.NET), through <see cref="PersistenceInstrumentation"/>.
/// </summary>
/// <remarks>
///     Registered by hand in <c>AddPersistence</c> (<c>AddInstrumentationDecorators</c>): the
///     application asks for <see cref="IUnitOfWork"/> and receives this decorator, with the
///     provider's unit of work inside. The command handlers never know it is there.
///     <para>
///         Each commit becomes one log line
///         (<c>Persistence Ado UnitOfWork.SaveChanges succeeded in 3.456 ms</c>) and one
///         measurement in the <c>cqrsapi.persistence.operation.duration</c> histogram, tagged with
///         provider, operation and outcome (succeeded, failed or cancelled). The decorator only
///         measures: exceptions pass through unchanged. A duplicate email, for example, still
///         reaches the handler as <c>UniqueConstraintViolationException</c> (and becomes a 409),
///         and is recorded here as <c>failed</c>.
///     </para>
///     <para>
///         This is the most telling measurement of the two providers: it covers the whole write,
///         the aggregates and their outbox rows in one transaction, including the retries after a
///         transient failure. It is what the EF Core / ADO.NET comparison mostly looks at.
///     </para>
/// </remarks>
internal sealed class UnitOfWorkInstrumentationDecorator(
    IUnitOfWork inner,
    PersistenceInstrumentation instrumentation) : IUnitOfWork
{
    // The "operation" tag of the logs and the metric: <interface without I>.<method without Async>.
    // Dashboards and queries filter by these values, so treat them as a contract.
    private const string SaveChangesOperation = "UnitOfWork.SaveChanges";

    /// <summary>The provider's implementation being timed (exposed for the DI tests).</summary>
    public IUnitOfWork Inner => inner;

    /// <summary>
    ///     Measures the whole commit: turning the domain events into outbox rows, then the
    ///     transaction (EF Core's SaveChanges or AdoUnitOfWork's batch), retries included.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return instrumentation.MeasureAsync(SaveChangesOperation, CallInner);

        // The real call, run (and timed) by MeasureAsync.
        Task CallInner() => inner.SaveChangesAsync(cancellationToken);
    }
}
