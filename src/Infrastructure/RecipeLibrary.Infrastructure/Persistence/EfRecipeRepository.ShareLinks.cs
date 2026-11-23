using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed partial class EfRecipeRepository
{
    public Task<Guid?> GetLinkKeyAsync(string ownerUserId, Guid recipeId, CancellationToken ct) =>
        OwnedRecipes(ownerUserId).Where(recipe => recipe.Id == recipeId)
            .Select(recipe => recipe.PublicShareKey).SingleOrDefaultAsync(ct);

    public async Task<Guid?> SetLinkAsync(string ownerUserId, Guid recipeId, bool enabled, CancellationToken ct)
    {
        var recipes = OwnedRecipes(ownerUserId).Where(recipe => recipe.Id == recipeId);
        if (!enabled)
        {
            await recipes.ExecuteUpdateAsync(setters => setters.SetProperty(recipe => recipe.PublicShareKey, (Guid?)null), ct);
            return null;
        }

        var key = Guid.NewGuid();
        recipes = CanBeShared(recipes);
        await recipes.Where(recipe => recipe.PublicShareKey == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(recipe => recipe.PublicShareKey, key), ct);
        return await recipes.Select(recipe => recipe.PublicShareKey).SingleOrDefaultAsync(ct);
    }

    public Task<Recipe?> GetSharedRecipeAsync(Guid key, CancellationToken ct) =>
        SharedRecipe(key).AsNoTracking()
            .Include(recipe => recipe.Ingredients)
            .Include(recipe => recipe.InstructionSteps)
            .AsSingleQuery()
            .SingleOrDefaultAsync(ct);

    public Task<string?> GetSharedImageUrlAsync(Guid key, CancellationToken ct) =>
        SharedRecipe(key).Select(recipe => recipe.ImageUrl).SingleOrDefaultAsync(ct);

    public Task<Guid?> FindSavedCopyAsync(string ownerUserId, Guid sourceId, CancellationToken ct) =>
        OwnedRecipes(ownerUserId).Where(recipe => recipe.SharedFromRecipeId == sourceId)
            .Select(recipe => (Guid?)recipe.Id).SingleOrDefaultAsync(ct);

    private IQueryable<Recipe> SharedRecipe(Guid key) =>
        CanBeShared(dbContext.Recipes)
            .Where(recipe => recipe.HouseholdId == null && recipe.PublicShareKey == key);

    private static IQueryable<Recipe> CanBeShared(IQueryable<Recipe> recipes) =>
        recipes.Where(recipe => recipe.ModerationStatus == ModerationStatus.NotModerated
            || recipe.ModerationStatus == ModerationStatus.Approved);
}
