using System.Net.Sockets;
using Microsoft.Data.SqlClient;

namespace RecipeLibrary.Infrastructure.Persistence;

/// <summary>
/// Detects SQL / network exceptions that are safe to retry during database warmup
/// (e.g. Azure SQL auto-pause error 40613).
/// </summary>
public static class SqlTransientExceptionDetector
{
    // Aligned with Azure SQL / EF Core transient numbers, plus common connectivity failures.
    private static readonly HashSet<int> TransientErrorNumbers =
    [
        -2,    // Client timeout
        20,    // Instance not found / connection broken
        64,    // Connection failed
        233,   // Connection initialization error
        10053, // Transport-level error
        10054, // Connection reset by peer
        10060, // Network timeout
        40197, // Service error processing request
        40501, // Service busy
        4060,  // Cannot open database (can occur while resuming)
        40613, // Database unavailable (auto-pause / resuming)
        10928, // Resource limit
        10929, // Resource limit
        4221,  // Login processing delayed
        49918, // Cannot process request
        49919, // Cannot process create/update
        49920, // Too many operations in progress
    ];

    public static bool IsTransient(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case TimeoutException:
                case SocketException:
                    return true;
                case SqlException sqlException when IsTransientSqlException(sqlException):
                    return true;
                case AggregateException aggregate when aggregate.InnerExceptions.Any(IsTransient):
                    return true;
            }
        }

        return false;
    }

    public static bool IsTransientSqlErrorNumber(int number) => TransientErrorNumbers.Contains(number);

    private static bool IsTransientSqlException(SqlException sqlException)
    {
        if (IsTransientSqlErrorNumber(sqlException.Number))
        {
            return true;
        }

        foreach (SqlError error in sqlException.Errors)
        {
            if (IsTransientSqlErrorNumber(error.Number))
            {
                return true;
            }
        }

        return false;
    }
}
