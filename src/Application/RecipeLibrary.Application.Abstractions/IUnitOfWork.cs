namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Runs work inside a single database transaction (compatible with EF retry strategy).
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
