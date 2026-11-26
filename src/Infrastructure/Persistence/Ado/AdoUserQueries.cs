using System.Data;
using CqrsApiExample.Application.Auth;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Domain.UserAggregate;
using Microsoft.Data.SqlClient;

namespace CqrsApiExample.Infrastructure.Persistence.Ado;

/// <summary>
///     ADO.NET version of <c>UserQueries</c>: reads straight from a
///     <see cref="SqlDataReader"/> into <see cref="UserDto"/>.
/// </summary>
/// <remarks>
///     Read side of CQRS: no aggregates, no change set and no unit of work, only rows mapped
///     to read models, the counterpart of EF Core's <c>AsNoTracking().Select(...)</c>.
///     Used when <c>Persistence:Provider</c> is <c>Ado</c>; the application receives it wrapped in
///     <c>UserQueriesInstrumentationDecorator</c>. The results (columns and order) must match
///     <c>UserQueries</c>: the same endpoint tests run against both providers.
/// </remarks>
internal sealed class AdoUserQueries(SqlConnectionFactory connections) : IUserQueries
{
    // Built at compile time from constants. The table is qualified with its schema (never dbo).
    private const string SelectColumns = $"SELECT [Id], [Name], [Surname], [Email] FROM [{Schemas.Accounts}].[Users]";

    // Oldest first, like UserQueries. CreatedAt is the clustered index, so the rows are already in this order.
    private const string GetAllSql = $"{SelectColumns} ORDER BY [CreatedAt]";

    // Uses the (non-clustered) primary key.
    private const string GetByIdSql = $"{SelectColumns} WHERE [Id] = @Id";

    // Column positions in SelectColumns: reading by ordinal skips the name lookup on every row,
    // so these must follow the SELECT list.
    private const int IdOrdinal = 0;
    private const int NameOrdinal = 1;
    private const int SurnameOrdinal = 2;
    private const int EmailOrdinal = 3;

    // Only what the login needs, never the rest of the user. Id is ordinal 0 here too.
    private const string GetCredentialsByEmailSql =
        $"SELECT [Id], [PasswordHash] FROM [{Schemas.Accounts}].[Users] WHERE [Email] = @Email";

    private const int PasswordHashOrdinal = 1;

    public Task<List<UserDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        // Reads change nothing, so retrying the whole attempt after a transient failure is safe.
        return AdoRetry.ExecuteAsync(GetAllOnceAsync, cancellationToken);

        async Task<List<UserDto>> GetAllOnceAsync(CancellationToken token)
        {
            // Pooled connection, opened for this query and returned to the pool at the end.
            await using SqlConnection connection = await connections.OpenAsync(token);
            await using SqlCommand command = new(GetAllSql, connection);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(token);

            List<UserDto> users = [];

            // Streams the rows: only one is in the reader at a time.
            while (await reader.ReadAsync(token))
            {
                UserDto user = Read(reader);
                users.Add(user);
            }

            return users;
        }
    }

    /// <summary>The user, or <c>null</c> when the id doesn't exist (the handler turns it into User.NotFound).</summary>
    public Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return AdoRetry.ExecuteAsync(GetByIdOnceAsync, cancellationToken);

        async Task<UserDto?> GetByIdOnceAsync(CancellationToken token)
        {
            await using SqlConnection connection = await connections.OpenAsync(token);
            await using SqlCommand command = new(GetByIdSql, connection);

            SqlParameter idParameter = new("@Id", SqlDbType.UniqueIdentifier)
            {
                Value = id
            };
            command.Parameters.Add(idParameter);

            // SingleRow tells SqlClient at most one row is expected (the id is the primary key).
            await using SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, token);

            bool found = await reader.ReadAsync(token);

            if (!found)
            {
                return null;
            }

            UserDto user = Read(reader);

            return user;
        }
    }

    /// <summary>
    ///     The login's lookup: id and password hash of the user with this (normalized) email,
    ///     or <c>null</c>. Uses the unique index on Email.
    /// </summary>
    public Task<UserCredentialsDto?> GetCredentialsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return AdoRetry.ExecuteAsync(GetCredentialsOnceAsync, cancellationToken);

        async Task<UserCredentialsDto?> GetCredentialsOnceAsync(CancellationToken token)
        {
            await using SqlConnection connection = await connections.OpenAsync(token);
            await using SqlCommand command = new(GetCredentialsByEmailSql, connection);

            // Sized like the column (nvarchar(256)), so every email length shares one query plan.
            SqlParameter emailParameter = new("@Email", SqlDbType.NVarChar, User.EmailMaxLength)
            {
                Value = email
            };
            command.Parameters.Add(emailParameter);

            await using SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, token);

            bool found = await reader.ReadAsync(token);

            if (!found)
            {
                return null;
            }

            Guid id = reader.GetGuid(IdOrdinal);
            string passwordHash = reader.GetString(PasswordHashOrdinal);

            UserCredentialsDto credentials = new(id, passwordHash);

            return credentials;
        }
    }

    /// <summary>Maps the current row. Every column is NOT NULL, so no DBNull checks are needed.</summary>
    private static UserDto Read(SqlDataReader reader)
    {
        Guid id = reader.GetGuid(IdOrdinal);
        string name = reader.GetString(NameOrdinal);
        string surname = reader.GetString(SurnameOrdinal);
        string email = reader.GetString(EmailOrdinal);

        return new UserDto(id, name, surname, email);
    }
}
