using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Abstractions;

public interface IRecipeRepository
{
    Task AddAsync(Recipe recipe, CancellationToken ct = default);

    Task<IReadOnlyList<Recipe>> GetListAsync(string? search, RecipeCategory? category, CancellationToken ct = default);

    Task<Recipe?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Recipe>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);

    Task<Recipe?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    Task UpdateAsync(Recipe recipe, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(Guid recipeId, CancellationToken ct = default);
}

