using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Application.Tests;

/// <summary>No-op unit of work for unit tests that do not assert persistence transactions.</summary>
internal sealed class NoOpUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        await action(ct);
        await SaveChangesAsync(ct);
    }
}
