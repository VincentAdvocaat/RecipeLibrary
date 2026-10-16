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
    private bool acquired;
    public async Task AcquireAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = 'RecipeLibrary.PublicDemo',
                    @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 15000;
                IF @result < 0 THROW 51000, 'Demo is busy. Please retry.', 1;
                """, ct);
            acquired = true;
        }
        catch
        {
            await db.Database.CloseConnectionAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (acquired)
                await db.Database.ExecuteSqlRawAsync("EXEC sp_releaseapplock @Resource = 'RecipeLibrary.PublicDemo', @LockOwner = 'Session';");
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
