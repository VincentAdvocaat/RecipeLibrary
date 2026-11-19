using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Infrastructure.Persistence;

namespace RecipeLibrary.Web.Demo;

/// <summary>
/// Serializes demo operations across replicas, including cleanup and reset.
/// The SQL session lock also covers durable attempt counters before the content transaction.
/// Normal accounts never acquire this lock.
/// </summary>
public sealed class DemoDatabaseLock(RecipeDbContext db) : IAsyncDisposable
{
    private SqlConnection? connection;

    public async Task AcquireAsync(CancellationToken ct)
    {
        // Circuit disposal can dispose the scoped DbContext while an operation unwinds.
        // Own the lock session independently. Never return a session-level lock to a pool,
        // even if acquisition is cancelled after SQL has already granted it.
        var settings = new SqlConnectionStringBuilder(db.Database.GetConnectionString()) { Pooling = false, ConnectRetryCount = 0 };
        connection = new SqlConnection(settings.ConnectionString);
        try
        {
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = 'RecipeLibrary.PublicDemo',
                    @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 15000;
                IF @result < 0 THROW 51000, 'Demo is busy. Please retry.', 1;
                """;
            await command.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            await DisposeAsync();
            // SqlClient can report an interrupted sp_getapplock as SqlException.
            ct.ThrowIfCancellationRequested();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Closing this non-pooled physical session releases every session-owned lock.
        var owned = Interlocked.Exchange(ref connection, null);
        if (owned is not null) await owned.DisposeAsync();
    }
}
