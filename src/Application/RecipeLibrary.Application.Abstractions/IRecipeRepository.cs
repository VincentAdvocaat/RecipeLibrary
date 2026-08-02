using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Abstractions;

public interface IRecipeRepository
{
    /// <summary>Tracks a new recipe; caller must <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
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

    /// <summary>
    /// Replaces the recipe header and children atomically (self-contained transaction).
    /// Does not require a subsequent <see cref="IUnitOfWork.SaveChangesAsync"/>.
    /// </summary>
    Task UpdateAsync(string ownerUserId, Recipe recipe, CancellationToken ct = default);

    /// <summary>
    /// Deletes the recipe and its children atomically (self-contained transaction).
    /// </summary>
    Task DeleteAsync(string ownerUserId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(
        string ownerUserId,
        Guid recipeId,
        CancellationToken ct = default);

    /// <summary>
    /// Images inherit ownership via the Recipe relation. Pending uploads (not yet linked to any recipe)
    /// are readable by any authenticated caller; linked images only by the owning user.
    /// </summary>
    Task<bool> IsRecipeImageAccessibleAsync(
        string ownerUserId,
        string fileName,
        CancellationToken ct = default);
}
