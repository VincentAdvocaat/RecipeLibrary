using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.Ingredients;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class AddIngredientTagsCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_NormalizesAndDeduplicatesTags()
    {
        var ingredientId = Guid.NewGuid();
        var repo = new FakeIngredientRepository();
        var sut = new AddIngredientTagsCommandHandler(repo, new IngredientTextNormalizer());

        var result = await sut.HandleAsync(new AddIngredientTagsCommand
        {
            IngredientId = ingredientId,
            Tags = [" Weekmenu ", "weekmenu", "", "  "],
        });

        Assert.Equal(1, result.AddedCount);
        Assert.Equal(ingredientId, repo.LastIngredientId);
        Assert.Single(repo.LastTags!);
        Assert.Equal("Weekmenu", repo.LastTags![0].Name);
    }

    private sealed class FakeIngredientRepository : IIngredientRepository
    {
        public Guid? LastIngredientId { get; private set; }
        public IReadOnlyList<(string Name, string NormalizedName)>? LastTags { get; private set; }

        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default)
        {
            LastIngredientId = ingredientId;
            LastTags = tags;
            return Task.CompletedTask;
        }

        public Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;
        public Task<CanonicalIngredient> CreateIngredientWithAliasAsync(string canonicalName, string normalizedName, string alias, string normalizedAlias, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CanonicalIngredient>>([]);
        public Task<CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, CancellationToken ct = default) => Task.FromResult<CanonicalIngredient?>(null);
        public Task<CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default) => Task.FromResult<CanonicalIngredient?>(null);
        public Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CanonicalIngredient>>([]);
        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tag>>([]);
    }
}
