using System.Data;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Domain.UserAggregate;
using Microsoft.Data.SqlClient;

namespace CqrsApiExample.Infrastructure.Persistence.Ado;

/// <summary>ADO.NET version of <c>UserRepository</c>: same contract, hand-written SQL.</summary>
/// <remarks>
///     Write side only: it exposes what the commands use and never saves. <see cref="Add"/>
///     stages the user in the scoped <see cref="AdoChangeSet"/>, and <see cref="AdoUnitOfWork"/>
///     inserts it (with its outbox rows) when the command handler commits. Used when
///     <c>Persistence:Provider</c> is <c>Ado</c>; the application receives it wrapped in
///     <c>UserRepositoryInstrumentationDecorator</c>.
/// </remarks>
internal sealed class AdoUserRepository(SqlConnectionFactory connections, AdoChangeSet changes) : IUserRepository
{
    // EXISTS stops at the first matching row (found through the unique index on Email), and the
    // CASE turns the answer into a bit, which SqlClient reads as a bool: one value, no rows to read.
    // The table is qualified with its schema: tables never live in dbo.
    private const string ExistsByEmailSql =
        $"""
         SELECT CASE WHEN EXISTS (SELECT 1 FROM [{Schemas.Accounts}].[Users] WHERE [Email] = @Email)
                     THEN CAST(1 AS bit)
                     ELSE CAST(0 AS bit)
                END
         """;

    /// <summary>
    ///     The fast duplicate-email check of <c>CreateUserCommandHandler</c>. It doesn't replace the
    ///     unique index: two concurrent requests can both get <c>false</c>, and the index rejects the second.
    /// </summary>
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        // A read changes nothing, so retrying the whole attempt after a transient failure is safe.
        // AdoRetry mirrors EF Core's EnableRetryOnFailure (3 retries, at most 5 s apart).
        return AdoRetry.ExecuteAsync(ExistsOnceAsync, cancellationToken);

        async Task<bool> ExistsOnceAsync(CancellationToken token)
        {
            // Pooled connection, opened for this query and returned to the pool at the end.
            await using SqlConnection connection = await connections.OpenAsync(token);
            await using SqlCommand command = new(ExistsByEmailSql, connection);

            // Explicit type and size, matching the column (nvarchar(256)): SqlClient would otherwise
            // size it by the value, and every email length would compile its own query plan.
            SqlParameter emailParameter = new("@Email", SqlDbType.NVarChar, User.EmailMaxLength)
            {
                Value = email
            };
            command.Parameters.Add(emailParameter);

            object? scalar = await command.ExecuteScalarAsync(token);
            bool exists = (bool)scalar;

            return exists;
        }
    }

    /// <summary>
    ///     Stages a new user; nothing is sent to the database here. The insert happens in
    ///     <see cref="AdoUnitOfWork.SaveChangesAsync"/>, in the same transaction as the user's
    ///     domain events (outbox), as <c>UserRepository.Add</c> + <c>UnitOfWork</c> do with EF Core.
    /// </summary>
    public void Add(User user)
    {
        changes.Add(user);
    }
}
