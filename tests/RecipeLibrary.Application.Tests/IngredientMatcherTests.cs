using Xunit;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Tests;

public sealed class IngredientMatcherTests
{
    [Fact]
    public async Task MatchAsync_UsesAliasBeforeFuzzy()
    {
        var repo = new FakeIngredientRepository(
            ingredients:
            [
                new CanonicalIngredient { Id = Guid.NewGuid(), CanonicalName = "gember", NormalizedName = "gember", CreatedAt = DateTimeOffset.UtcNow }
            ],
            aliases: new Dictionary<string, string> { ["verse gember"] = "gember" });

        var sut = new IngredientMatcher(repo, new IngredientTextNormalizer());
        var result = await sut.MatchAsync("verse gember");

        Assert.Equal("alias", result.MatchType);
        Assert.Equal("gember", result.Ingredient?.CanonicalName);
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

        var sut = new IngredientMatcher(repo, new IngredientTextNormalizer());
        var result = await sut.MatchAsync("gembre");

        Assert.Equal("fuzzy", result.MatchType);
        Assert.Equal("gember", result.Ingredient?.CanonicalName);
        Assert.True(result.Confidence > 0.7m);
    }

    private sealed class FakeIngredientRepository(
        IReadOnlyList<CanonicalIngredient> ingredients,
        IReadOnlyDictionary<string, string> aliases)
        : IIngredientRepository
    {
        public Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;

        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) => Task.CompletedTask;

        public Task<CanonicalIngredient> CreateIngredientWithAliasAsync(string canonicalName, string normalizedName, string alias, string normalizedAlias, CancellationToken ct = default)
            => Task.FromResult(new CanonicalIngredient { Id = Guid.NewGuid(), CanonicalName = canonicalName, NormalizedName = normalizedName, CreatedAt = DateTimeOffset.UtcNow });

        public Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default)
            => Task.FromResult(ingredients);

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

        public Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default)
            => Task.FromResult(ingredients);

        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tag>>([]);
    }
}
