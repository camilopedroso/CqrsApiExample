using System.Data;
using System.Text;
using CqrsApiExample.Application.Common.Exceptions;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Domain.Common.Models;
using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;

namespace CqrsApiExample.Infrastructure.Persistence.Ado;

/// <summary>How <see cref="AdoUnitOfWork"/> sends its statements to SQL Server.</summary>
internal enum AdoWriteMode
{
    /// <summary>
    ///     One command holding the transaction and every INSERT: a single round trip,
    ///     like EF Core's command batching (the default).
    /// </summary>
    Batched,

    /// <summary>
    ///     A client-side transaction and one command per INSERT: BEGIN, each INSERT and
    ///     COMMIT are separate round trips. Kept to measure what batching saves.
    /// </summary>
    CommandPerStatement
}

/// <summary>
///     ADO.NET version of <c>UnitOfWork</c>. Inserts the aggregates added in this
///     scope and their domain events (as outbox rows) in one transaction, so the
///     outbox guarantee is the same as with EF Core: the events exist if and only
///     if the changes were committed. <see cref="AdoWriteMode"/> decides whether
///     that takes one round trip or one per statement.
/// </summary>
/// <remarks>
///     Used when <c>Persistence:Provider</c> is <c>Ado</c>. Scoped, like the
///     <see cref="AdoChangeSet"/> it shares with <c>AdoUserRepository</c>: the repository only
///     adds to the change set, and this class writes it. Only inserts are supported today;
///     a new aggregate type needs a case in <see cref="Insert(AggregateRoot, string)"/>.
///     The EF migrations own the schema, so the SQL below must follow them.
/// </remarks>
internal sealed class AdoUnitOfWork(
    SqlConnectionFactory connections,
    AdoChangeSet changes,
    TimeProvider timeProvider,
    AdoWriteMode mode = AdoWriteMode.Batched) : IUnitOfWork
{
    // Exposed so tests can check which mode DI picked (always Batched).
    public AdoWriteMode Mode => mode;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        List<AggregateRoot> aggregates = [.. changes.Added];

        if (aggregates.Count == 0)
        {
            return;
        }

        // Read once, so the aggregates and their outbox rows share the same timestamp.
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        // There is no interceptor here: CreatedAt is set by reflection, the job
        // AuditableEntityInterceptor does for EF Core.
        aggregates.ForEach(aggregate => AuditFields.SetCreatedAt(aggregate, now));

        // Same serialization as the EF unit of work: (full type name, JSON).
        // Built once, outside the retry, so every attempt inserts the same message ids.
        List<IDomainEvent> domainEvents = [.. aggregates.SelectMany(aggregate => aggregate.GetDomainEvents())];
        List<OutboxMessage> outboxMessages = [.. domainEvents.Select(domainEvent => ToOutboxMessage(domainEvent, now))];

        try
        {
            // Retries the whole unit (new connection, new transaction, every INSERT) on
            // transient failures, never one command inside a transaction that already failed.
            await AdoRetry.ExecuteAsync(SaveOnceAsync, cancellationToken);
        }
        catch (SqlException ex) when (SqlServerErrors.IsUniqueViolation(ex))
        {
            // 2601/2627 (for example a duplicate email): the transaction was rolled back, so
            // neither the user nor its outbox row exists. Translated into the Application's
            // exception, as the EF unit of work does; not transient, so never retried.
            throw new UniqueConstraintViolationException(ex.Message, ex);
        }

        // Only after a successful commit: if the save failed, the aggregates keep their
        // events and the change set keeps them, so nothing is lost before the request ends.
        // (The EF unit of work clears the events before saving.)
        aggregates.ForEach(aggregate => aggregate.ClearDomainEvents());
        changes.Clear();

        // One attempt: builds the statements, opens a connection and runs them in one transaction.
        async Task SaveOnceAsync(CancellationToken token)
        {
            // Built on every attempt: a SqlParameter can't be added to a second command.
            // Each statement gets its own parameter prefix (a0_, a1_, m0_...), so they can
            // share one command in Batched mode.
            List<Statement> aggregateStatements = [.. aggregates.Select(InsertAggregate)];
            List<Statement> outboxStatements = [.. outboxMessages.Select(InsertOutboxMessage)];
            List<Statement> statements = [.. aggregateStatements, .. outboxStatements];

            // Pooled connection, opened per attempt and returned to the pool at the end.
            await using SqlConnection connection = await connections.OpenAsync(token);

            if (mode == AdoWriteMode.Batched)
            {
                await ExecuteBatchedAsync(statements, connection, token);
            }
            else
            {
                await ExecuteCommandPerStatementAsync(statements, connection, token);
            }
        }

        static Statement InsertAggregate(AggregateRoot aggregate, int index)
        {
            string prefix = $"a{index}_";

            return Insert(aggregate, prefix);
        }

        static Statement InsertOutboxMessage(OutboxMessage message, int index)
        {
            string prefix = $"m{index}_";

            return Insert(message, prefix);
        }
    }

    /// <summary>
    ///     One round trip. XACT_ABORT ON is what makes this safe: any error (e.g. a
    ///     unique violation) aborts the batch and rolls the transaction back.
    ///     Without it a failed INSERT only ends that statement, and the COMMIT
    ///     would store the outbox row of a user that was never saved.
    ///     SQL Server accepts up to 2100 parameters per command (about 190 users
    ///     with their events), far more than a unit of work saves today.
    /// </summary>
    private static async Task ExecuteBatchedAsync(List<Statement> statements,
                                                  SqlConnection connection,
                                                  CancellationToken cancellationToken)
    {
        StringBuilder sql = new();
        sql.AppendLine("SET XACT_ABORT ON;");
        sql.AppendLine("BEGIN TRANSACTION;");
        statements.ForEach(statement => sql.AppendLine(statement.Sql));
        sql.AppendLine("COMMIT TRANSACTION;");

        string sqlString = sql.ToString();
        SqlParameter[] parameters = [.. statements.SelectMany(statement => statement.Parameters)];

        await using SqlCommand command = new(sqlString, connection);
        command.Parameters.AddRange(parameters);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteCommandPerStatementAsync(List<Statement> statements,
                                                              SqlConnection connection,
                                                              CancellationToken cancellationToken)
    {
        // Disposing an uncommitted transaction rolls it back.
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (Statement statement in statements)
        {
            await using SqlCommand command = new(statement.Sql, connection, transaction);
            command.Parameters.AddRange(statement.Parameters);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    // NextAttemptAt = now: ready for OutboxProcessor right away.
    private static OutboxMessage ToOutboxMessage(IDomainEvent domainEvent, DateTime now)
    {
        (string type, string content) = OutboxSerializer.Serialize(domainEvent);

        OutboxMessage message = new()
        {
            Type = type,
            Content = content,
            OccurredAt = now,
            NextAttemptAt = now
        };

        return message;
    }

    // The ADO.NET counterpart of EF's model: one case per aggregate type that can be saved.
    // Fails loudly instead of silently skipping an aggregate it doesn't know.
    private static Statement Insert(AggregateRoot aggregate, string prefix)
    {
        if (aggregate is User user)
        {
            return InsertUser(user, prefix);
        }

        string typeName = aggregate.GetType().Name;
        throw new NotSupportedException($"The ADO.NET provider can't insert '{typeName}' yet.");
    }

    // Parameter names carry a prefix, so several statements can share one command.
    // Table names are qualified with their schema (never dbo). Explicit SqlDbType and sizes,
    // taken from the entity's constants, match the columns and keep the query plan stable.
    private static Statement InsertUser(User user, string p)
    {
        string sql = $"""
                      INSERT INTO [{Schemas.Accounts}].[Users] ([Id], [Name], [Surname], [Email], [CreatedAt], [ModifiedAt])
                      VALUES (@{p}Id, @{p}Name, @{p}Surname, @{p}Email, @{p}CreatedAt, NULL);
                      """;

        SqlParameter[] parameters =
        [
            Parameter($"@{p}Id", SqlDbType.UniqueIdentifier, user.Id),
            Parameter($"@{p}Name", SqlDbType.NVarChar, user.Name, User.NameMaxLength),
            Parameter($"@{p}Surname", SqlDbType.NVarChar, user.Surname, User.SurnameMaxLength),
            Parameter($"@{p}Email", SqlDbType.NVarChar, user.Email, User.EmailMaxLength),
            // DateTime2 explicitly: the default DateTime mapping would round to 3.33 ms.
            Parameter($"@{p}CreatedAt", SqlDbType.DateTime2, user.CreatedAt)
        ];

        return new Statement(sql, parameters);
    }

    // A new message: not leased, not processed, no attempts yet (LockedUntil, ProcessedAt and
    // Error stay NULL). Sizes follow OutboxMessageConfiguration: Type is nvarchar(256),
    // Content is nvarchar(max) (size -1).
    private static Statement Insert(OutboxMessage message, string p)
    {
        string sql = $"""
                      INSERT INTO [{Schemas.Messaging}].[OutboxMessages] ([Id], [Type], [Content], [OccurredAt], [NextAttemptAt], [Attempts])
                      VALUES (@{p}Id, @{p}Type, @{p}Content, @{p}OccurredAt, @{p}NextAttemptAt, 0);
                      """;

        SqlParameter[] parameters =
        [
            Parameter($"@{p}Id", SqlDbType.UniqueIdentifier, message.Id),
            Parameter($"@{p}Type", SqlDbType.NVarChar, message.Type, 256),
            Parameter($"@{p}Content", SqlDbType.NVarChar, message.Content, -1),
            Parameter($"@{p}OccurredAt", SqlDbType.DateTime2, message.OccurredAt),
            Parameter($"@{p}NextAttemptAt", SqlDbType.DateTime2, message.NextAttemptAt)
        ];

        return new Statement(sql, parameters);
    }

    private static SqlParameter Parameter(string name, SqlDbType type, object value, int size = 0)
    {
        SqlParameter parameter = new(name, type, size) { Value = value };

        return parameter;
    }

    // One INSERT and its parameters; the write mode decides how statements are sent.
    private sealed record Statement(string Sql, SqlParameter[] Parameters);
}
