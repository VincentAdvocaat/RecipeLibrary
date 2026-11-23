using System.Data;
using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed partial class EfRecipeRepository(RecipeDbContext dbContext)
    : IRecipeRepository, IRecipeRatingRepository, IHouseholdRecipeRepository
{
    public async Task AddAsync(Recipe recipe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipe.OwnerUserId);

        await dbContext.Recipes.AddAsync(recipe, ct);
    }

    public Task<IReadOnlyList<Recipe>> GetListAsync(
        string ownerUserId,
        string? search,
        RecipeCategory? category,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        return LoadListAsync(OwnedRecipes(ownerUserId), search, category, ct);
    }

    public Task<IReadOnlyList<Recipe>> GetHouseholdListAsync(
        string userId, Guid householdId, string? search, RecipeCategory? category, CancellationToken ct) =>
        LoadListAsync(AccessibleRecipes(dbContext, userId).Where(r => r.HouseholdId == householdId), search, category, ct);

    private static async Task<IReadOnlyList<Recipe>> LoadListAsync(
        IQueryable<Recipe> query, string? search, RecipeCategory? category, CancellationToken ct)
    {
        query = query.Include(r => r.Ingredients).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            // Double-cast forces the provider (string) type so Contains translates to SQL LIKE
            // against the converted Title column (EF Core value-converter limitation).
            query = query.Where(r =>
                ((string)(object)r.Title).Contains(term) ||
                (r.Description != null && r.Description.Contains(term)));
        }

        if (category is { } cat)
        {
            query = query.Where(r => r.Category == cat);
        }

        return await query.OrderBy(r => r.Title).ToListAsync(ct);
    }

    public Task<Recipe?> GetByIdAsync(string ownerUserId, Guid id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

        return AccessibleRecipes(dbContext, ownerUserId)
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .Include(r => r.InstructionSteps)
            .Include(r => r.Ratings.Where(rating => rating.UserId == ownerUserId))
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public Task<bool> TrySetRatingAsync(string userId, Guid recipeId, int stars, CancellationToken ct = default) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = dbContext.Database.CurrentTransaction is null
                ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            if (!await AccessibleRecipes(dbContext, userId).AnyAsync(r => r.Id == recipeId, ct))
            {
                return false;
            }

            await SetRatingAsync(userId, recipeId, stars, ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
            return true;
        });

    private async Task SetRatingAsync(string userId, Guid recipeId, int stars, CancellationToken ct)
    {
        if (stars == 0)
        {
            await dbContext.RecipeRatings
                .Where(r => r.RecipeId == recipeId && r.UserId == userId)
                .ExecuteDeleteAsync(ct);
            return;
        }

        // Hold the key range until the statement finishes so concurrent first ratings
        // for this user and recipe cannot both take the insert path.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                MERGE [RecipeRatings] WITH (HOLDLOCK) AS rating
                USING (VALUES ({recipeId}, {userId}, {stars})) AS incoming ([RecipeId], [UserId], [Stars])
                    ON rating.[RecipeId] = incoming.[RecipeId] AND rating.[UserId] = incoming.[UserId]
                WHEN MATCHED THEN
                    UPDATE SET [Stars] = incoming.[Stars]
                WHEN NOT MATCHED THEN
                    INSERT ([RecipeId], [UserId], [Stars])
                    VALUES (incoming.[RecipeId], incoming.[UserId], incoming.[Stars]);
                """, ct);
    }

    public async Task<IReadOnlyList<Recipe>> GetByIdsAsync(
        string ownerUserId,
        IReadOnlyList<Guid> ids,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

        if (ids.Count == 0)
        {
            return [];
        }

        return await AccessibleRecipes(dbContext, ownerUserId)
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(ct);
    }

    public Task<Recipe?> GetByIdForUpdateAsync(string ownerUserId, Guid id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

        return AccessibleRecipes(dbContext, ownerUserId)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task UpdateAsync(string ownerUserId, Recipe recipe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

        // Blazor Server reuses the scoped DbContext: detach stale rows from earlier saves
        // so SaveChanges does not try to DELETE children that were already removed via ExecuteDelete.
        DetachTrackedRecipeGraph(recipe.Id);

        // Self-contained write: ExecuteDelete/Update + AddRange must commit together so a
        // failed flush cannot leave a recipe with an updated header and zero children.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = dbContext.Database.CurrentTransaction is null
                ? await dbContext.Database.BeginTransactionAsync(ct) : null;
            try
            {
                var updated = await AccessibleRecipes(dbContext, ownerUserId)
                    .Where(r => r.Id == recipe.Id)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(r => r.Title, recipe.Title)
                            .SetProperty(r => r.Description, recipe.Description)
                            .SetProperty(r => r.SourceUrl, recipe.SourceUrl)
                            .SetProperty(r => r.ImageUrl, recipe.ImageUrl)
                            .SetProperty(r => r.PreparationMinutes, recipe.PreparationMinutes)
                            .SetProperty(r => r.CookingMinutes, recipe.CookingMinutes)
                            .SetProperty(r => r.Category, recipe.Category)
                            .SetProperty(r => r.Servings, recipe.Servings)
                            .SetProperty(r => r.Difficulty, recipe.Difficulty)
                            .SetProperty(r => r.UpdatedAt, recipe.UpdatedAt)
                            .SetProperty(r => r.ModerationStatus, recipe.ModerationStatus)
                            .SetProperty(r => r.ModeratedAt, recipe.ModeratedAt)
                            .SetProperty(r => r.ModerationSummary, recipe.ModerationSummary),
                        ct);

                if (updated == 0)
                {
                    return;
                }

                await dbContext.RecipeIngredients
                    .Where(x => x.RecipeId == recipe.Id)
                    .ExecuteDeleteAsync(ct);
                await dbContext.InstructionSteps
                    .Where(x => x.RecipeId == recipe.Id)
                    .ExecuteDeleteAsync(ct);
                dbContext.RecipeIngredients.AddRange(recipe.Ingredients);
                dbContext.InstructionSteps.AddRange(recipe.InstructionSteps);

                await dbContext.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                dbContext.ChangeTracker.Clear();
            }
            catch
            {
                if (transaction is not null) await transaction.RollbackAsync(ct);
                dbContext.ChangeTracker.Clear();
                throw;
            }
        });
    }

    public async Task DeleteAsync(string ownerUserId, Guid id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

        DetachTrackedRecipeGraph(id);

        await AccessibleRecipes(dbContext, ownerUserId).Where(r => r.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(
        string ownerUserId,
        Guid recipeId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

        var owned = await AccessibleRecipes(dbContext, ownerUserId).AnyAsync(r => r.Id == recipeId, ct);
        if (!owned)
        {
            return [];
        }

        return await (
            from ri in dbContext.RecipeIngredients.AsNoTracking()
            join it in dbContext.IngredientTags on ri.IngredientId equals it.IngredientId
            join tag in dbContext.Tags on it.TagId equals tag.Id
            where ri.RecipeId == recipeId && ri.IngredientId != null
            select tag.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(ct);
    }

    public async Task<bool> IsRecipeImageAccessibleAsync(
        string ownerUserId,
        string fileName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var linkedRecipes = dbContext.Recipes
            .AsNoTracking()
            .Where(r => r.ImageUrl != null
                        && (r.ImageUrl == fileName
                            || r.ImageUrl.EndsWith("/" + fileName)
                            || r.ImageUrl.EndsWith("\\" + fileName)
                            || r.ImageUrl.Contains("/" + fileName)
                            || r.ImageUrl.Contains(fileName)));

        // Pending uploads remain available for authenticated preview.
        var accessibleIds = AccessibleRecipes(dbContext, ownerUserId).Select(r => r.Id);
        var accessible = await linkedRecipes
            .Select(r => accessibleIds.Contains(r.Id))
            .Distinct()
            .ToListAsync(ct);
        return accessible.Count == 0 || accessible.Contains(true);
    }

    private IQueryable<Recipe> OwnedRecipes(string ownerUserId) =>
        dbContext.Recipes.Where(r => r.HouseholdId == null && r.OwnerUserId == ownerUserId);

    internal static IQueryable<Recipe> AccessibleRecipes(RecipeDbContext dbContext, string userId) =>
        dbContext.Recipes.Where(recipe => recipe.HouseholdId == null
            ? recipe.OwnerUserId == userId
            : dbContext.HouseholdMembers.Any(member => member.UserId == userId && member.HouseholdId == recipe.HouseholdId));

    private void DetachTrackedRecipeGraph(Guid recipeId)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<Ingredient>()
                     .Where(e => e.Entity.RecipeId == recipeId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<InstructionStep>()
                     .Where(e => e.Entity.RecipeId == recipeId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        var recipeEntry = dbContext.ChangeTracker.Entries<Recipe>()
            .FirstOrDefault(e => e.Entity.Id == recipeId);

        if (recipeEntry is not null)
        {
            recipeEntry.State = EntityState.Detached;
        }
    }
}
