using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.Ingredients;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class SearchTagsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_MapsTagsToLookupItems()
    {
        var tagId = Guid.NewGuid();
        var repo = new FakeIngredientRepository(
        [
            new Tag { Id = tagId, Name = "Weekmenu", NormalizedName = "weekmenu" },
        ]);

        var sut = new SearchTagsQueryHandler(repo, new IngredientTextNormalizer());
        var result = await sut.HandleAsync(new SearchTagsQuery { Query = "week" });

        Assert.Single(result);
        Assert.Equal(tagId, result[0].Id);
        Assert.Equal("Weekmenu", result[0].Name);
    }

    private sealed class FakeIngredientRepository(IReadOnlyList<Tag> tags) : IIngredientRepository
    {
        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default)
        {
            var matches = tags
                .Where(t => t.NormalizedName.Contains(normalizedQuery, StringComparison.Ordinal))
                .Take(take)
                .ToList();
            return Task.FromResult<IReadOnlyList<Tag>>(matches);
        }

        public Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) => Task.CompletedTask;
        public Task<CanonicalIngredient> CreateIngredientWithAliasAsync(string canonicalName, string normalizedName, string alias, string normalizedAlias, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CanonicalIngredient>>([]);
        public Task<CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, CancellationToken ct = default) => Task.FromResult<CanonicalIngredient?>(null);
        public Task<CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default) => Task.FromResult<CanonicalIngredient?>(null);
        public Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CanonicalIngredient>>([]);
    }
}
