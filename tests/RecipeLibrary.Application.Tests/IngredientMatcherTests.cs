using Xunit;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Tests;

public sealed class IngredientMatcherTests
{
    private static readonly IReadOnlyList<CanonicalIngredient> GehaktIngredients =
    [
        new CanonicalIngredient { Id = Guid.NewGuid(), CanonicalName = "gehakt", NormalizedName = "gehakt", CreatedAt = DateTimeOffset.UtcNow },
        new CanonicalIngredient { Id = Guid.NewGuid(), CanonicalName = "runder gehakt", NormalizedName = "runder gehakt", CreatedAt = DateTimeOffset.UtcNow },
    ];

    [Fact]
    public async Task MatchAsync_UsesAliasBeforeFuzzy()
    {
        var repo = new FakeIngredientRepository(
            ingredients:
            [
                new CanonicalIngredient { Id = Guid.NewGuid(), CanonicalName = "gember", NormalizedName = "gember", CreatedAt = DateTimeOffset.UtcNow }
            ],
            aliases: new Dictionary<string, string> { ["verse gember"] = "gember" });

        var result = await CreateMatcher(repo).MatchAsync("verse gember");

        Assert.Equal("alias", result.MatchType);
        Assert.Equal("gember", result.Ingredient?.CanonicalName);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public async Task MatchAsync_ReturnsExactMatch_WhenCanonicalNameIsPopulated()
    {
        var repo = new FakeIngredientRepository(
            ingredients:
            [
                new CanonicalIngredient { Id = Guid.NewGuid(), CanonicalName = "tomaat", NormalizedName = "tomaat", CreatedAt = DateTimeOffset.UtcNow }
            ],
            aliases: new Dictionary<string, string>());

        var result = await CreateMatcher(repo).MatchAsync("tomaat");

        Assert.Equal("exact", result.MatchType);
        Assert.Equal("tomaat", result.Ingredient?.CanonicalName);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public async Task MatchAsync_ReturnsFuzzyMatch_WhenAboveThreshold()
    {
        var repo = new FakeIngredientRepository(
            ingredients:
            [
                new CanonicalIngredient { Id = Guid.NewGuid(), CanonicalName = "gember", NormalizedName = "gember", CreatedAt = DateTimeOffset.UtcNow }
            ],
            aliases: new Dictionary<string, string>());

        var result = await CreateMatcher(repo).MatchAsync("gembre");

        Assert.Equal("fuzzy", result.MatchType);
        Assert.Equal("gember", result.Ingredient?.CanonicalName);
        Assert.True(result.Confidence > IngredientMatcher.FuzzyMatchScore);
        Assert.True(result.RequiresConfirmation);
        Assert.Contains(result.Suggestions, x => x.Ingredient.CanonicalName == "gember");
    }

    [Fact]
    public async Task MatchAsync_RequiresConfirmation_WhenCloseSuggestionsExist()
    {
        var repo = new FakeIngredientRepository(
            ingredients:
            [
                new CanonicalIngredient { Id = Guid.NewGuid(), CanonicalName = "gember", NormalizedName = "gember", CreatedAt = DateTimeOffset.UtcNow }
            ],
            aliases: new Dictionary<string, string>());

        var result = await CreateMatcher(repo).MatchAsync("gembr");

        Assert.True(result.RequiresConfirmation);
        Assert.NotEmpty(result.Suggestions);
    }

    [Fact]
    public async Task MatchAsync_DoesNotRequireConfirmation_WhenNoCloseSuggestionsExist()
    {
        var repo = new FakeIngredientRepository(
            ingredients:
            [
                new CanonicalIngredient { Id = Guid.NewGuid(), CanonicalName = "gember", NormalizedName = "gember", CreatedAt = DateTimeOffset.UtcNow }
            ],
            aliases: new Dictionary<string, string>());

        var result = await CreateMatcher(repo).MatchAsync("xyzabc123");

        Assert.Equal("none", result.MatchType);
        Assert.False(result.RequiresConfirmation);
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public async Task MatchAsync_FiltersSuggestionsBelowMinScore()
    {
        var repo = new FakeIngredientRepository(
            ingredients:
            [
                new CanonicalIngredient { Id = Guid.NewGuid(), CanonicalName = "gember", NormalizedName = "gember", CreatedAt = DateTimeOffset.UtcNow },
                new CanonicalIngredient { Id = Guid.NewGuid(), CanonicalName = "aardappel", NormalizedName = "aardappel", CreatedAt = DateTimeOffset.UtcNow },
            ],
            aliases: new Dictionary<string, string>());

        var result = await CreateMatcher(repo).MatchAsync("xyzabc123");

        Assert.All(result.Suggestions, x => Assert.True(x.Score >= IngredientMatcher.SuggestionMinScore));
    }

    [Fact]
    public async Task MatchAsync_SuggestsGehaktAndRunderGehakt_WhenInputIsGehak()
    {
        var repo = new FakeIngredientRepository(GehaktIngredients, new Dictionary<string, string>());

        var result = await CreateMatcher(repo).MatchAsync("gehak");

        Assert.True(result.RequiresConfirmation);
        Assert.Contains(result.Suggestions, x => x.Ingredient.CanonicalName == "gehakt");
        Assert.Contains(result.Suggestions, x => x.Ingredient.CanonicalName == "runder gehakt");
        Assert.All(result.Suggestions, x => Assert.True(x.Score >= IngredientMatcher.SuggestionMinScore));
    }

    [Fact]
    public async Task MatchAsync_SuggestsRunderGehakt_WhenInputIsGehakt()
    {
        var repo = new FakeIngredientRepository(
            [GehaktIngredients[1]],
            new Dictionary<string, string>());

        var result = await CreateMatcher(repo).MatchAsync("gehakt");

        Assert.Contains(result.Suggestions, x => x.Ingredient.CanonicalName == "runder gehakt");
        Assert.True(result.RequiresConfirmation);
    }

    [Fact]
    public async Task MatchAsync_SuggestsGehakt_WhenInputIsRunderGehakt()
    {
        var repo = new FakeIngredientRepository(
            [GehaktIngredients[0]],
            new Dictionary<string, string>());

        var result = await CreateMatcher(repo).MatchAsync("runder gehakt");

        Assert.Contains(result.Suggestions, x => x.Ingredient.CanonicalName == "gehakt");
        Assert.True(result.RequiresConfirmation);
    }

    private static IngredientMatcher CreateMatcher(IIngredientRepository repo) =>
        new(repo, new IngredientTextNormalizer(), new IngredientSimilarityScorer());

    private sealed class FakeIngredientRepository(
        IReadOnlyList<CanonicalIngredient> ingredients,
        IReadOnlyDictionary<string, string> aliases)
        : IIngredientRepository
    {
        public Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;

        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) => Task.CompletedTask;

        public Task<CanonicalIngredient> CreateIngredientWithAliasAsync(string canonicalName, string normalizedName, string alias, string normalizedAlias, CancellationToken ct = default)
            => Task.FromResult(new CanonicalIngredient { Id = Guid.NewGuid(), CanonicalName = canonicalName, NormalizedName = normalizedName, CreatedAt = DateTimeOffset.UtcNow });

        public Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CanonicalIngredient>>(
                ingredients
                    .Where(x => IngredientCandidateMatcher.Matches(normalizedQuery, x.NormalizedName))
                    .Take(take)
                    .ToList());

        public Task<CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, CancellationToken ct = default)
        {
            if (!aliases.TryGetValue(normalizedAlias, out var canonical))
            {
                return Task.FromResult<CanonicalIngredient?>(null);
            }

            return Task.FromResult(ingredients.FirstOrDefault(x => x.NormalizedName == canonical));
        }

        public Task<CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default)
            => Task.FromResult(ingredients.SingleOrDefault(x => x.NormalizedName == normalizedName));

        public Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CanonicalIngredient>>(
                string.IsNullOrWhiteSpace(normalizedQuery)
                    ? ingredients.Take(take).ToList()
                    : ingredients
                        .Where(x => IngredientCandidateMatcher.Matches(normalizedQuery, x.NormalizedName))
                        .Take(take)
                        .ToList());

        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tag>>([]);
    }
}
