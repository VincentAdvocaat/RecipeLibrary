using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Abstractions;

public interface IRecipeRepository
{
    Task AddAsync(Recipe recipe, CancellationToken ct = default);

    Task<IReadOnlyList<Recipe>> GetListAsync(
        string ownerUserId,
        string? search,
        RecipeCategory? category,
        CancellationToken ct = default);

    Task<Recipe?> GetByIdAsync(string ownerUserId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Recipe>> GetByIdsAsync(
        string ownerUserId,
        IReadOnlyList<Guid> ids,
        CancellationToken ct = default);

    Task<Recipe?> GetByIdForUpdateAsync(string ownerUserId, Guid id, CancellationToken ct = default);

    Task UpdateAsync(string ownerUserId, Recipe recipe, CancellationToken ct = default);

    Task DeleteAsync(string ownerUserId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(
        string ownerUserId,
        Guid recipeId,
        CancellationToken ct = default);

    /// <summary>
    /// Images inherit ownership via the Recipe relation. Pending uploads (not yet linked) are
    /// readable only when the storage key is prefixed with the caller's user id.
    /// </summary>
    Task<bool> IsRecipeImageAccessibleAsync(
        string ownerUserId,
        string fileName,
        CancellationToken ct = default);
}
