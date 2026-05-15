using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.Ingredients;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class SearchIngredientsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsCanonicalName_NotEmpty()
    {
        var tomatoId = Guid.NewGuid();
        var repo = new CanonicalIngredientRepository(
        [
            new CanonicalIngredient
            {
                Id = tomatoId,
                CanonicalName = "tomaat",
                NormalizedName = "tomaat",
                CreatedAt = DateTimeOffset.UtcNow,
            },
        ]);

        var sut = new SearchIngredientsQueryHandler(repo, new IngredientTextNormalizer());
        var result = await sut.HandleAsync(new SearchIngredientsQuery { Query = "toma" });

        Assert.Single(result);
        Assert.Equal("tomaat", result[0].Name);
        Assert.Equal(tomatoId, result[0].Id);
    }

    private sealed class CanonicalIngredientRepository(IReadOnlyList<CanonicalIngredient> ingredients)
        : IIngredientRepository
    {
        public Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;

        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) => Task.CompletedTask;

        public Task<CanonicalIngredient> CreateIngredientWithAliasAsync(string canonicalName, string normalizedName, string alias, string normalizedAlias, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default)
            => Task.FromResult(ingredients);

        public Task<CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, CancellationToken ct = default)
            => Task.FromResult<CanonicalIngredient?>(null);

        public Task<CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default)
            => Task.FromResult(ingredients.SingleOrDefault(x => x.NormalizedName == normalizedName));

        public Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default)
        {
            var matches = ingredients
                .Where(x => x.NormalizedName.Contains(normalizedQuery, StringComparison.Ordinal))
                .Take(take)
                .ToList();
            return Task.FromResult<IReadOnlyList<CanonicalIngredient>>(matches);
        }

        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tag>>([]);
    }
}
