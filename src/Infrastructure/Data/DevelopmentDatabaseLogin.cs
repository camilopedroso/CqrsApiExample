using System.Data;
using CqrsApiExample.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace CqrsApiExample.Infrastructure.Data;

/// <summary>
///     Development only: makes sure the login in <c>ConnectionStrings:Database</c> exists and is a
///     member of <see cref="DatabaseRoles.Application"/>, so the API can run with least privilege on
///     a fresh container without any manual step.
/// </summary>
/// <remarks>
///     <para>
///         Runs over the migrations connection (<c>ConnectionStrings:Migrations</c>, the <c>sa</c>
///         login in Development) right after the migrations, because creating a login needs server
///         rights the API itself must not have. Outside Development a DBA or the deployment creates
///         the login once and adds its user to the role; nothing here runs.
///     </para>
///     <para>
///         Skipped when the API connects as the same login as the migrations (as the tests do with
///         <c>sa</c>) or with integrated security (no login to create). An existing login keeps its
///         password: this never changes credentials, it only fills in what is missing.
///     </para>
/// </remarks>
internal static class DevelopmentDatabaseLogin
{
    // Login and user names can't be parameters in DDL, so the statement is built inside SQL Server
    // with QUOTENAME (brackets for names, quotes for the password): values never become SQL syntax.
    private const string EnsureLoginSql =
        """
        IF SUSER_ID(@login) IS NULL
        BEGIN
            DECLARE @createLogin nvarchar(max) =
                N'CREATE LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = ' + QUOTENAME(@password, '''') +
                N', DEFAULT_DATABASE = ' + QUOTENAME(DB_NAME()) + N', CHECK_POLICY = ON;';
            EXEC (@createLogin);
        END;

        IF DATABASE_PRINCIPAL_ID(@login) IS NULL
        BEGIN
            DECLARE @createUser nvarchar(max) = N'CREATE USER ' + QUOTENAME(@login) + N' FOR LOGIN ' + QUOTENAME(@login) + N';';
            EXEC (@createUser);
        END;

        IF IS_ROLEMEMBER(@role, @login) = 0
        BEGIN
            DECLARE @addMember nvarchar(max) = N'ALTER ROLE ' + QUOTENAME(@role) + N' ADD MEMBER ' + QUOTENAME(@login) + N';';
            EXEC (@addMember);
        END;
        """;

    public static async Task EnsureAsync(string migrationsConnectionString,
                                         string applicationConnectionString,
                                         CancellationToken cancellationToken)
    {
        SqlConnectionStringBuilder migrations = new(migrationsConnectionString);
        SqlConnectionStringBuilder application = new(applicationConnectionString);

        bool hasOwnLogin = !application.IntegratedSecurity
                           && !string.IsNullOrEmpty(application.UserID)
                           && !string.Equals(application.UserID, migrations.UserID, StringComparison.OrdinalIgnoreCase);

        if (!hasOwnLogin)
        {
            return;
        }

        // Connected to the application's database as the migrations login, so CREATE USER and
        // ALTER ROLE apply there (CREATE LOGIN is server-wide wherever it runs).
        await using SqlConnection connection = new(migrationsConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using SqlCommand command = new(EnsureLoginSql, connection);

        SqlParameter[] parameters =
        [
            new("@login", SqlDbType.NVarChar, 128) { Value = application.UserID },
            new("@password", SqlDbType.NVarChar, 128) { Value = application.Password },
            new("@role", SqlDbType.NVarChar, 128) { Value = DatabaseRoles.Application }
        ];
        command.Parameters.AddRange(parameters);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
