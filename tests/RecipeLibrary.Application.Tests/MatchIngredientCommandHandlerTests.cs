using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.Ingredients;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class MatchIngredientCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_LogsMatchAndReturnsExactResult()
    {
        var gehaktId = Guid.NewGuid();
        var repo = new FakeIngredientRepository(IngredientTestFactory.Create("Gehakt", id: gehaktId));

        var matcher = new IngredientMatcher(repo, new IngredientTextNormalizer(), new IngredientSimilarityScorer());
        var sut = new MatchIngredientCommandHandler(matcher, repo);

        var result = await sut.HandleAsync(new MatchIngredientCommand { Input = "Gehakt" });

        Assert.Equal("exact", result.MatchType);
        Assert.False(result.RequiresConfirmation);
        Assert.NotNull(result.Ingredient);
        Assert.Equal("Gehakt", result.Ingredient!.Name);
        Assert.Equal("nl", result.Ingredient.LanguageCode);
        Assert.NotNull(repo.LastLog);
        Assert.Equal("Gehakt", repo.LastLog!.Input);
    }

    private sealed class FakeIngredientRepository(CanonicalIngredient? exactMatch) : IIngredientRepository
    {
        public IngredientMatchLog? LastLog { get; private set; }

        public Task<CanonicalIngredient?> GetByNormalizedNameAsync(
            string normalizedName,
            IReadOnlyList<string> languageCodes,
            CancellationToken ct = default) =>
            Task.FromResult(
                exactMatch is not null
                && exactMatch.Translations.Any(t => t.NormalizedDisplayName == normalizedName)
                    ? exactMatch
                    : null);

        public Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default)
        {
            LastLog = log;
            return Task.CompletedTask;
        }

        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) => Task.CompletedTask;

        public Task<CanonicalIngredient> FindOrCreateAsync(
            string languageCode,
            string displayName,
            string normalizedDisplayName,
            string? alias,
            string? normalizedAlias,
            CancellationToken ct = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(
            string normalizedQuery,
            IReadOnlyList<string> languageCodes,
            int take,
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CanonicalIngredient>>([]);

        public Task<CanonicalIngredient?> GetByNormalizedAliasAsync(
            string normalizedAlias,
            IReadOnlyList<string> languageCodes,
            CancellationToken ct = default) => Task.FromResult<CanonicalIngredient?>(null);

        public Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(
            string normalizedQuery,
            IReadOnlyList<string> languageCodes,
            int take,
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CanonicalIngredient>>([]);

        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tag>>([]);
    }
}
