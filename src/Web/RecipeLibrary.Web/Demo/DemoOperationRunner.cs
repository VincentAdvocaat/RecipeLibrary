using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Infrastructure.Persistence;

namespace RecipeLibrary.Web.Demo;

public sealed class DemoOperationRunner(
    RecipeDbContext db, ICurrentUser user, IDemoContext context,
    IOptions<DemoOptions> options, TimeProvider time, ILogger<DemoOperationRunner> logger) : IDemoOperationRunner
{
    private readonly SemaphoreSlim operations = new(1, 1);
    private sealed record DemoReadOperation;

    public Task<TResult> RunReadAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct) =>
        RunAsync(new DemoReadOperation(), action, ct);

    public static bool IsAllowed(object operation) => operation is
        DemoReadOperation or GetRecipeListQuery or GetRecipeByIdQuery or GetRecipeIngredientTagsQuery or
        CreateRecipeCommand or UpdateRecipeCommand or DeleteRecipeCommand or
        SearchIngredientsQuery or SearchTagsQuery or MatchIngredientCommand or
        EnsureShoppingListGroupCommand or GetNextShoppingListNameQuery or GetShoppingListSummaryQuery or
        AddRecipesToShoppingListCommand or ToggleShoppingListItemCommand or RemoveShoppingListItemCommand or
        ClearShoppingListCommand or DeleteShoppingListCommand or DeleteShoppingListGroupCommand or
        SplitShoppingListCommand or MoveShoppingListItemCommand or UpdateShoppingListNameCommand or
        UpdateShoppingListItemQuantityCommand or AddManualShoppingListItemCommand or
        GetPantryItemsQuery or UpsertPantryItemCommand or RemovePantryItemCommand or
        ApplyPantryToShoppingListCommand or MoveShoppingListItemToPantryCommand;

    public async Task<TResult> RunAsync<TResult>(object operation, Func<Task<TResult>> action, CancellationToken ct)
    {
        if (!context.IsDemo)
        {
            // Interactive events can overlap within one scoped DbContext.
            await operations.WaitAsync(ct);
            try { return await action(); }
            finally { operations.Release(); }
        }
        if (!IsAllowed(operation)) return Denied<TResult>("Demo.AccountRequired");

        await operations.WaitAsync(ct);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var gate = new DemoDatabaseLock(db);
                await gate.AcquireAsync(ct);
                db.ChangeTracker.Clear();
                var session = await GetValidSessionAsync(ct);
                if (operation is EnsureShoppingListGroupCommand ensure && ensure.OwnerUserId != user.UserId)
                    return Denied<TResult>("Demo.AccountRequired");
                if (operation is CreateRecipeCommand && await db.Recipes.CountAsync(x => x.OwnerUserId == user.UserId, ct) >= options.Value.MaxRecipes)
                    return Denied<TResult>("Demo.Limit");
                if (operation is CreateRecipeCommand create) ValidateRecipe(create.ImageUrl, create.Ingredients.Count, create.InstructionSteps.Count);
                if (operation is UpdateRecipeCommand update) ValidateRecipe(update.ImageUrl, update.Ingredients.Count, update.InstructionSteps.Count);

                if (operation is CreateRecipeCommand or UpdateRecipeCommand)
                {
                    if (session.SaveAttempts >= options.Value.MaxSaveAttempts) return Denied<TResult>("Demo.Limit");
                    session.SaveAttempts++;
                    // Keep attempts even when validation, moderation or the later transaction fails.
                    await db.SaveChangesAsync(ct);
                }

                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    var result = await action();
                    await db.SaveChangesAsync(ct);
                    await CheckCountsAsync(session.UserId, ct);
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
        finally { operations.Release(); }
    }

    public async Task<DemoSession> GetValidSessionAsync(CancellationToken ct = default)
    {
        if (!context.IsDemo || !options.Value.Enabled || user.UserId is null)
            throw new DemoAccessException("Demo.Expired");
        var session = await db.DemoSessions.SingleOrDefaultAsync(x => x.UserId == user.UserId, ct);
        if (session is null || session.ExpiresAt <= time.GetUtcNow()) throw new DemoAccessException("Demo.Expired");
        if (session.Revision != context.Revision) throw new DemoAccessException("Demo.Reload");
        return session;
    }

    private void ValidateRecipe(string? image, int ingredients, int steps)
    {
        if ((!string.IsNullOrEmpty(image) && !DemoSeed.IsImage(image)) || ingredients > 100 || steps > 100)
            throw new DemoAccessException("Demo.Limit");
    }

    private async Task CheckCountsAsync(string owner, CancellationToken ct)
    {
        var limits = options.Value;
        var groups = db.ShoppingListGroups.Where(x => x.OwnerUserId == owner).Select(x => x.Id);
        var lists = db.ShoppingLists.Where(x => groups.Contains(x.GroupId));
        var listIds = lists.Select(x => x.Id);
        if (await db.Recipes.CountAsync(x => x.OwnerUserId == owner, ct) > limits.MaxRecipes
            || await lists.CountAsync(ct) > limits.MaxLists
            || await db.PantryItems.CountAsync(x => x.OwnerUserId == owner, ct) > limits.MaxPantryItems
            || await db.ShoppingListItems.Where(x => listIds.Contains(x.ShoppingListId))
                .GroupBy(x => x.ShoppingListId).AnyAsync(x => x.Count() > limits.MaxItemsPerList, ct))
            throw new DemoAccessException("Demo.Limit");
    }

    private T Denied<T>(string code)
    {
        logger.LogInformation("Demo operation refused: {Code}", code);
        throw new DemoAccessException(code);
    }
}
