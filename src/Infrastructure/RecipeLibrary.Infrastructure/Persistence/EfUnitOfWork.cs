using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class EfUnitOfWork(RecipeDbContext dbContext) : IUnitOfWork
{
    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            await action(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
