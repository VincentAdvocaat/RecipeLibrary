using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Abstractions;

public interface IHouseholdRecipeRepository
{
    Task<IReadOnlyList<Recipe>> GetHouseholdListAsync(
        string userId, Guid householdId, string? search, RecipeCategory? category, CancellationToken ct);

    Task<IReadOnlySet<Guid>> GetSharedSourceIdsAsync(string userId, Guid householdId, CancellationToken ct);

    Task<ShareRecipesWithHouseholdResult> ShareAsync(
        string userId, Guid householdId, IReadOnlyList<Guid> recipeIds, CancellationToken ct);
}
