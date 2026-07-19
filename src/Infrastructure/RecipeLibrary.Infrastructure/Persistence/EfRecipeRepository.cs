using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class EfRecipeRepository(RecipeDbContext dbContext) : IRecipeRepository
{
    public async Task AddAsync(Recipe recipe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipe.OwnerUserId);

        await dbContext.Recipes.AddAsync(recipe, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Recipe>> GetListAsync(
        string ownerUserId,
        string? search,
        RecipeCategory? category,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

        var query = OwnedRecipes(ownerUserId)
            .Include(r => r.Ingredients)
            .AsNoTracking();

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

        return OwnedRecipes(ownerUserId)
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .Include(r => r.InstructionSteps)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
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

        return await OwnedRecipes(ownerUserId)
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(ct);
    }

    public Task<Recipe?> GetByIdForUpdateAsync(string ownerUserId, Guid id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

        return OwnedRecipes(ownerUserId)
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

        var owned = await OwnedRecipes(ownerUserId).AnyAsync(r => r.Id == recipe.Id, ct);
        if (!owned)
        {
            return;
        }

        await dbContext.RecipeIngredients
            .Where(x => x.RecipeId == recipe.Id)
            .ExecuteDeleteAsync(ct);

        await dbContext.InstructionSteps
            .Where(x => x.RecipeId == recipe.Id)
            .ExecuteDeleteAsync(ct);

        if (recipe.Ingredients.Count > 0)
        {
            await dbContext.RecipeIngredients.AddRangeAsync(recipe.Ingredients, ct);
        }

        if (recipe.InstructionSteps.Count > 0)
        {
            await dbContext.InstructionSteps.AddRangeAsync(recipe.InstructionSteps, ct);
        }

        if (recipe.Ingredients.Count > 0 || recipe.InstructionSteps.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }

        await OwnedRecipes(ownerUserId)
            .Where(r => r.Id == recipe.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.Title, recipe.Title)
                    .SetProperty(r => r.Description, recipe.Description)
                    .SetProperty(r => r.ImageUrl, recipe.ImageUrl)
                    .SetProperty(r => r.PreparationMinutes, recipe.PreparationMinutes)
                    .SetProperty(r => r.CookingMinutes, recipe.CookingMinutes)
                    .SetProperty(r => r.Category, recipe.Category)
                    .SetProperty(r => r.Servings, recipe.Servings)
                    .SetProperty(r => r.Difficulty, recipe.Difficulty)
                    .SetProperty(r => r.UpdatedAt, recipe.UpdatedAt),
                ct);
    }

    public async Task DeleteAsync(string ownerUserId, Guid id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

        DetachTrackedRecipeGraph(id);

        var owned = await OwnedRecipes(ownerUserId).AnyAsync(r => r.Id == id, ct);
        if (!owned)
        {
            return;
        }

        await dbContext.InstructionSteps
            .Where(s => s.RecipeId == id)
            .ExecuteDeleteAsync(ct);

        await dbContext.RecipeIngredients
            .Where(i => i.RecipeId == id)
            .ExecuteDeleteAsync(ct);

        await OwnedRecipes(ownerUserId)
            .Where(r => r.Id == id)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(
        string ownerUserId,
        Guid recipeId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

        var owned = await OwnedRecipes(ownerUserId).AnyAsync(r => r.Id == recipeId, ct);
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

        var linkedOwners = await dbContext.Recipes
            .AsNoTracking()
            .Where(r => r.ImageUrl != null
                        && (r.ImageUrl == fileName
                            || r.ImageUrl.EndsWith("/" + fileName)
                            || r.ImageUrl.EndsWith("\\" + fileName)
                            || r.ImageUrl.Contains("/" + fileName)
                            || r.ImageUrl.Contains(fileName)))
            .Select(r => r.OwnerUserId)
            .Distinct()
            .ToListAsync(ct);

        if (linkedOwners.Count == 0)
        {
            // Pending upload not yet attached to a recipe — allow authenticated preview.
            return true;
        }

        return linkedOwners.Contains(ownerUserId);
    }

    private IQueryable<Recipe> OwnedRecipes(string ownerUserId) =>
        dbContext.Recipes.Where(r => r.OwnerUserId == ownerUserId);

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
