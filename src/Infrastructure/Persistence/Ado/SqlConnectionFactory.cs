using Microsoft.Data.SqlClient;

namespace CqrsApiExample.Infrastructure.Persistence.Ado;

/// <summary>
///     Opens SQL Server connections for the ADO.NET provider. Each operation
///     opens its own connection and disposes it right away; SqlClient's
///     connection pool makes that cheap.
/// </summary>
internal sealed class SqlConnectionFactory(string connectionString)
{
    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
