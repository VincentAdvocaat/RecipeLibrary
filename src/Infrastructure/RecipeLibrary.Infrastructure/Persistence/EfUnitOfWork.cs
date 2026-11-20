using Microsoft.EntityFrameworkCore;
using System.Data;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class EfUnitOfWork(RecipeDbContext dbContext) : IUnitOfWork
{
    public async Task ExecuteInSerializableTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (dbContext.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("A serializable operation must own its transaction.");

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var snapshot = dbContext.ChangeTracker.Entries().Select(entry =>
                (Entry: entry, State: entry.State, Current: entry.CurrentValues.Clone(), Original: entry.OriginalValues.Clone(),
                    Modified: entry.Properties.Where(property => property.IsModified).Select(property => property.Metadata.Name).ToHashSet()))
                .ToList();
            var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var committed = false;
            try
            {
                await action(ct);
                await dbContext.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
                await transaction.CommitAsync(ct);
                committed = true;
                dbContext.ChangeTracker.AcceptAllChanges();
            }
            catch
            {
                // A deadlock or broken connection may already have ended the transaction.
                // Preserve the action failure so EF can recognize retryable SQL errors.
                try { await transaction.RollbackAsync(CancellationToken.None); }
                catch { }
                foreach (var entry in dbContext.ChangeTracker.Entries().ToList())
                    if (!snapshot.Any(saved => ReferenceEquals(saved.Entry.Entity, entry.Entity)))
                        entry.State = EntityState.Detached;
                foreach (var saved in snapshot)
                {
                    saved.Entry.CurrentValues.SetValues(saved.Current);
                    saved.Entry.OriginalValues.SetValues(saved.Original);
                    saved.Entry.State = saved.State;
                    if (saved.State == EntityState.Modified)
                        foreach (var property in saved.Entry.Properties)
                            property.IsModified = saved.Modified.Contains(property.Metadata.Name);
                }
                throw;
            }
            finally
            {
                try { await transaction.DisposeAsync(); }
                catch when (!committed) { }
            }
        });
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        dbContext.SaveChangesAsync(ct);

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (dbContext.Database.CurrentTransaction is not null)
            return action(ct);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            await action(ct);
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
