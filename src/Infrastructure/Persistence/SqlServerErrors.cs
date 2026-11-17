using Microsoft.Data.SqlClient;

namespace CqrsApiExample.Infrastructure.Persistence;

/// <summary>SQL Server error numbers the persistence layer reacts to, shared by both providers.</summary>
internal static class SqlServerErrors
{
    // 2601 = duplicate key in a unique index, 2627 = unique/primary key constraint.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    // Transient failures worth retrying: the main numbers EF Core's SqlServerTransientExceptionDetector
    // retries (that class is internal EF API, so the ADO provider keeps its own list).
    private static readonly HashSet<int> TransientErrorNumbers =
    [
        -2,    // client timeout
        20,    // the instance does not support encryption (connection dropped during login)
        64,    // connection broken during login
        121,   // semaphore timeout
        233,   // no process on the other end of the pipe
        1205,  // deadlock victim
        4060,  // cannot open database (e.g. during a failover)
        10053, // transport-level error: connection aborted
        10054, // transport-level error: connection reset
        10060, // network timeout
        40197, // Azure SQL: error processing the request
        40501, // Azure SQL: service busy
        40613, // Azure SQL: database unavailable
        49918, // Azure SQL: not enough resources
        49919, // Azure SQL: too many operations in progress
        49920  // Azure SQL: service busy
    ];

    public static bool IsUniqueViolation(SqlException exception) =>
        exception.Number is UniqueIndexViolation or UniqueConstraintViolation;

    public static bool IsTransient(Exception exception) => exception switch
    {
        SqlException sql => sql.Errors.Cast<SqlError>().Any(error => TransientErrorNumbers.Contains(error.Number)),
        TimeoutException => true,
        _ => false
    };
}
