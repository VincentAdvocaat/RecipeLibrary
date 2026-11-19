using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Infrastructure.Identity;
using RecipeLibrary.Infrastructure.Persistence;

namespace RecipeLibrary.Web.Demo;

public sealed class DemoSessionService(RecipeDbContext db, UserManager<ApplicationUser> users,
    IOptions<DemoOptions> options, TimeProvider time, ILogger<DemoSessionService> logger)
{
    public Task<DemoSession> StartAsync(string address, CancellationToken ct) =>
        LockedAsync(async () =>
        {
            if (!options.Value.Enabled) throw new DemoAccessException("Demo.Disabled");
            var now = time.GetUtcNow();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address)));
            var quota = await db.DemoStartQuotas.SingleOrDefaultAsync(x => x.AddressHash == hash, ct);
            if (quota is null)
            {
                quota = new DemoStartQuota { AddressHash = hash, WindowStart = now };
                db.DemoStartQuotas.Add(quota);
            }
            if (quota.WindowStart <= now.AddHours(-1)) { quota.WindowStart = now; quota.Starts = 0; }
            if (quota.Starts >= options.Value.StartsPerIpPerHour
                || await db.DemoSessions.CountAsync(x => x.ExpiresAt > now, ct) >= options.Value.MaxActiveSessions)
                throw new DemoAccessException("Demo.Limit");

            var id = Guid.NewGuid().ToString("N");
            var user = new ApplicationUser { Id = id, UserName = $"demo-{id}", Email = $"{id}@demo.invalid" };
            var created = await users.CreateAsync(user);
            if (!created.Succeeded) throw new InvalidOperationException("Could not create demo identity.");
            var session = new DemoSession { UserId = id, CreatedAt = now, ExpiresAt = now.AddMinutes(options.Value.LifetimeMinutes), SeedVersion = DemoSeed.Version };
            db.DemoSessions.Add(session);
            quota.Starts++;
            await DemoSeed.PopulateAsync(db, id, now, ct);
            logger.LogInformation("Demo started; expires at {ExpiresAt}", session.ExpiresAt);
            return session;
        }, ct);

    public Task<DemoSession> ResetAsync(string owner, int revision, CancellationToken ct) =>
        LockedAsync(async () =>
        {
            var session = await RequireAsync(owner, revision, ct);
            if (session.ResetCount >= options.Value.MaxResets) throw new DemoAccessException("Demo.Limit");
            await RemoveContentAsync(owner, ct);
            session.ResetCount++;
            session.Revision++;
            session.SeedVersion = DemoSeed.Version;
            await DemoSeed.PopulateAsync(db, owner, time.GetUtcNow(), ct);
            logger.LogInformation("Demo reset");
            return session;
        }, ct);

    public Task<bool> EndAsync(string owner, int revision, CancellationToken ct) =>
        LockedAsync(async () =>
        {
            await RequireAsync(owner, revision, ct);
            await RemoveUserAsync(owner, ct);
            logger.LogInformation("Demo ended");
            return true;
        }, ct);

    public Task<int> CleanupAsync(CancellationToken ct) => LockedAsync(async () =>
    {
        var now = time.GetUtcNow();
        var owners = await db.DemoSessions.Where(x => x.ExpiresAt <= now).Select(x => x.UserId).ToListAsync(ct);
        foreach (var owner in owners) await RemoveUserAsync(owner, ct);
        await db.DemoStartQuotas.Where(x => x.WindowStart <= now.AddHours(-1)).ExecuteDeleteAsync(ct);
        if (owners.Count > 0) logger.LogInformation("Removed {Count} expired demos", owners.Count);
        return owners.Count;
    }, ct);

    private async Task<DemoSession> RequireAsync(string owner, int revision, CancellationToken ct)
    {
        var session = await db.DemoSessions.SingleOrDefaultAsync(x => x.UserId == owner, ct);
        if (!options.Value.Enabled || session is null || session.ExpiresAt <= time.GetUtcNow()) throw new DemoAccessException("Demo.Expired");
        if (session.Revision != revision) throw new DemoAccessException("Demo.Reload");
        return session;
    }

    private async Task RemoveContentAsync(string owner, CancellationToken ct)
    {
        var recipes = db.Recipes.Where(x => x.OwnerUserId == owner).Select(x => x.Id);
        await db.ContentReports.Where(x => x.ReporterUserId == owner || recipes.Contains(x.RecipeId)).ExecuteDeleteAsync(ct);
        await db.ContentModerationEvents.Where(x => x.DemoOwnerUserId == owner || (x.RecipeId != null && recipes.Contains(x.RecipeId.Value))).ExecuteDeleteAsync(ct);
        await db.ShoppingListGroups.Where(x => x.OwnerUserId == owner).ExecuteDeleteAsync(ct);
        await db.PantryItems.Where(x => x.OwnerUserId == owner).ExecuteDeleteAsync(ct);
        await db.Recipes.Where(x => x.OwnerUserId == owner).ExecuteDeleteAsync(ct);
    }

    private async Task RemoveUserAsync(string owner, CancellationToken ct)
    {
        await RemoveContentAsync(owner, ct);
        await db.DemoSessions.Where(x => x.UserId == owner).ExecuteDeleteAsync(ct);
        await db.Users.Where(x => x.Id == owner).ExecuteDeleteAsync(ct);
    }

    private Task<T> LockedAsync<T>(Func<Task<T>> action, CancellationToken ct) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        await using var gate = new DemoDatabaseLock(db);
        await gate.AcquireAsync(ct);
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw;
        }
    });
}
