namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Application-owned persistence boundary. Mutating command handlers must take a non-optional
/// <see cref="IUnitOfWork"/> and call <see cref="SaveChangesAsync"/> (or
/// <see cref="ExecuteInTransactionAsync"/>) after tracked repository writes.
/// Some repository methods are self-contained (own transaction) — see port XML docs.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="action"/> inside a single database transaction
    /// (compatible with EF retry strategy), then saves and commits.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
