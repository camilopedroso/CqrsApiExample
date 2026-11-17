using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Infrastructure.Persistence.Instrumentation;

namespace CqrsApiExample.Infrastructure.Persistence.Decorators;

/// <summary>
///     Times every <see cref="IUserRepository"/> call of whichever provider is configured
///     (EF Core or ADO.NET), through <see cref="PersistenceInstrumentation"/>.
/// </summary>
/// <remarks>
///     Registered by hand in <c>AddPersistence</c> (<c>AddInstrumentationDecorators</c>): the
///     application asks for <see cref="IUserRepository"/> and receives this decorator, with the
///     provider's repository inside. <c>CreateUserCommandHandler</c> never knows it is there.
///     <para>
///         Each call becomes one log line
///         (<c>Persistence EfCore UserRepository.ExistsByEmail succeeded in 1.234 ms</c>) and one
///         measurement in the <c>cqrsapi.persistence.operation.duration</c> histogram, tagged with
///         provider, operation and outcome (succeeded, failed or cancelled). The decorator only
///         measures: exceptions pass through unchanged, and the global exception handler logs them.
///     </para>
///     <para>
///         Measuring here, outside the providers, is what makes the EF Core / ADO.NET comparison
///         fair: both are timed by the same code, at the same point.
///     </para>
/// </remarks>
internal sealed class UserRepositoryInstrumentationDecorator(IUserRepository inner,
                                                             PersistenceInstrumentation instrumentation)
    : IUserRepository
{
    // The "operation" tag of the logs and the metric: <interface without I>.<method without Async>.
    // Dashboards and queries filter by these values, so treat them as a contract.
    private const string ExistsByEmailOperation = "UserRepository.ExistsByEmail";
    private const string AddOperation = "UserRepository.Add";

    /// <summary>The provider's implementation being timed (exposed for the DI tests).</summary>
    public IUserRepository Inner => inner;

    /// <summary>
    ///     Measures the database round trip of the duplicate-email check, until the result arrives.
    /// </summary>
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return instrumentation.MeasureAsync(ExistsByEmailOperation, CallInner);

        // The real call, run (and timed) by MeasureAsync.
        Task<bool> CallInner() => inner.ExistsByEmailAsync(email, cancellationToken);
    }

    /// <summary>
    ///     Measures only the in-memory part: neither provider touches the database in Add
    ///     (EF Core starts tracking the user, ADO.NET stages it in the change set). The insert
    ///     is measured by <c>UnitOfWorkInstrumentationDecorator</c> as UnitOfWork.SaveChanges.
    /// </summary>
    public void Add(User user)
    {
        instrumentation.Measure(AddOperation, CallInner);

        // The real call, run (and timed) by Measure.
        void CallInner() => inner.Add(user);
    }
}
