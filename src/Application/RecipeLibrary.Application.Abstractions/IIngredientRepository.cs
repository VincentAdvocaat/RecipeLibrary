using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Abstractions;

public interface IIngredientRepository
{
    Task<CanonicalIngredient?> GetByNormalizedNameAsync(
        string normalizedName,
        IReadOnlyList<string> languageCodes,
        CancellationToken ct = default);

    Task<CanonicalIngredient?> GetByNormalizedAliasAsync(
        string normalizedAlias,
        IReadOnlyList<string> languageCodes,
        CancellationToken ct = default);

    Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(
        string normalizedQuery,
        IReadOnlyList<string> languageCodes,
        int take,
        CancellationToken ct = default);

    Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(
        string normalizedQuery,
        IReadOnlyList<string> languageCodes,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Finds an existing ingredient by normalized display name in the given language,
    /// or creates one. Race-safe under concurrent callers.
    /// </summary>
    Task<CanonicalIngredient> FindOrCreateAsync(
        string languageCode,
        string displayName,
        string normalizedDisplayName,
        string? alias,
        string? normalizedAlias,
        CancellationToken ct = default);

    Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default);

    Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default);

    Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default);
}
