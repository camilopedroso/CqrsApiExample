using CqrsApiExample.Application.Auth;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Common.Paging;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Infrastructure.Persistence.Instrumentation;

namespace CqrsApiExample.Infrastructure.Persistence.Decorators;

/// <summary>
///     Times every <see cref="IUserQueries"/> call of whichever provider is configured
///     (EF Core or ADO.NET), through <see cref="PersistenceInstrumentation"/>.
/// </summary>
/// <remarks>
///     Registered by hand in <c>AddPersistence</c> (<c>AddInstrumentationDecorators</c>): the
///     application asks for <see cref="IUserQueries"/> and receives this decorator, with the
///     provider's queries inside. The query handlers never know it is there.
///     <para>
///         Each call becomes one log line
///         (<c>Persistence Ado UserQueries.GetPage succeeded in 1.234 ms</c>) and one
///         measurement in the <c>cqrsapi.persistence.operation.duration</c> histogram, tagged with
///         provider, operation and outcome (succeeded, failed or cancelled). The decorator only
///         measures: exceptions pass through unchanged, and the global exception handler logs them.
///     </para>
///     <para>
///         These are the read side's numbers: projections straight to <see cref="UserDto"/>, with no
///         change tracking, so they show the raw cost of each provider's query and mapping.
///     </para>
/// </remarks>
internal sealed class UserQueriesInstrumentationDecorator(IUserQueries inner,
                                                          PersistenceInstrumentation instrumentation)
    : IUserQueries
{
    // The "operation" tag of the logs and the metric: <interface without I>.<method without Async>.
    // Dashboards and queries filter by these values, so treat them as a contract.
    private const string GetPageOperation = "UserQueries.GetPage";
    private const string GetByIdOperation = "UserQueries.GetById";
    private const string GetCredentialsByEmailOperation = "UserQueries.GetCredentialsByEmail";

    /// <summary>The provider's implementation being timed (exposed for the DI tests).</summary>
    public IUserQueries Inner => inner;

    /// <summary>
    ///     Measures the count, the page query and the mapping of its rows, until the page is in memory.
    /// </summary>
    public Task<PagedResult<UserDto>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        return instrumentation.MeasureAsync(GetPageOperation, CallInner);

        // The real call, run (and timed) by MeasureAsync.
        Task<PagedResult<UserDto>> CallInner() => inner.GetPageAsync(page, pageSize, cancellationToken);
    }

    /// <summary>
    ///     Measures the lookup by primary key. An id that doesn't exist is still <c>succeeded</c>:
    ///     the query ran and returned <c>null</c>; the handler turns that into User.NotFound.
    /// </summary>
    public Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return instrumentation.MeasureAsync(GetByIdOperation, CallInner);

        // The real call, run (and timed) by MeasureAsync.
        Task<UserDto?> CallInner() => inner.GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    ///     Measures the login's lookup by email (the unique index). An unknown email is still
    ///     <c>succeeded</c>; the handler turns it into Auth.InvalidCredentials.
    /// </summary>
    public Task<UserCredentialsDto?> GetCredentialsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return instrumentation.MeasureAsync(GetCredentialsByEmailOperation, CallInner);

        // The real call, run (and timed) by MeasureAsync.
        Task<UserCredentialsDto?> CallInner() => inner.GetCredentialsByEmailAsync(email, cancellationToken);
    }
}
