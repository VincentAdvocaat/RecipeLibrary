using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Abstractions;

public interface IIngredientRepository
{
    Task<CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default);

    Task<CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, CancellationToken ct = default);

    Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default);

    Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default);

    Task<CanonicalIngredient> CreateIngredientWithAliasAsync(
        string canonicalName,
        string normalizedName,
        string alias,
        string normalizedAlias,
        CancellationToken ct = default);

    Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default);

    Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default);

    Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default);
}
