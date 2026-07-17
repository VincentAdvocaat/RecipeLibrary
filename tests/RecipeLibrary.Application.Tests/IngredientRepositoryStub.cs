using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Tests;

/// <summary>
/// Default no-op / empty implementations for ingredient repository fakes in tests.
/// </summary>
internal abstract class IngredientRepositoryStub : IIngredientRepository
{
    public virtual Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default) =>
        Task.CompletedTask;

    public virtual Task AddTagsAsync(
        Guid ingredientId,
        IReadOnlyList<(string Name, string NormalizedName)> tags,
        CancellationToken ct = default) =>
        Task.CompletedTask;

    public virtual Task<CanonicalIngredient> FindOrCreateAsync(
        string languageCode,
        string displayName,
        string normalizedDisplayName,
        string? alias,
        string? normalizedAlias,
        CancellationToken ct = default) =>
        throw new NotImplementedException();

    public virtual Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(
        string normalizedQuery,
        IReadOnlyList<string> languageCodes,
        int take,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CanonicalIngredient>>([]);

    public virtual Task<CanonicalIngredient?> GetByNormalizedAliasAsync(
        string normalizedAlias,
        IReadOnlyList<string> languageCodes,
        CancellationToken ct = default) =>
        Task.FromResult<CanonicalIngredient?>(null);

    public virtual Task<CanonicalIngredient?> GetByNormalizedNameAsync(
        string normalizedName,
        IReadOnlyList<string> languageCodes,
        CancellationToken ct = default) =>
        Task.FromResult<CanonicalIngredient?>(null);

    public virtual Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(
        string normalizedQuery,
        IReadOnlyList<string> languageCodes,
        int take,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CanonicalIngredient>>([]);

    public virtual Task<IReadOnlyList<Tag>> SearchTagsAsync(
        string normalizedQuery,
        int take,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Tag>>([]);
}
