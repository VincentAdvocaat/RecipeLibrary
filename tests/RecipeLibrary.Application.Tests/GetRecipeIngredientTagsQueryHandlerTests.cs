using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class GetRecipeIngredientTagsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsTags_FromRepository()
    {
        var recipeId = Guid.NewGuid();
        var repo = new FakeRecipeRepository(["weekmenu", "snel"]);
        var sut = new GetRecipeIngredientTagsQueryHandler(repo);

        var result = await sut.HandleAsync(new GetRecipeIngredientTagsQuery { RecipeId = recipeId });

        Assert.Equal(2, result.Count);
        Assert.Contains("weekmenu", result);
        Assert.Equal(recipeId, repo.LastRecipeId);
    }

    private sealed class FakeRecipeRepository(IReadOnlyList<string> tags) : IRecipeRepository
    {
        public Guid? LastRecipeId { get; private set; }

        public Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(Guid recipeId, CancellationToken ct = default)
        {
            LastRecipeId = recipeId;
            return Task.FromResult(tags);
        }

        public Task AddAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Recipe?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Recipe?>(null);
        public Task<Recipe?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Recipe?>(null);
        public Task<IReadOnlyList<Recipe>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Recipe>>([]);
        public Task<IReadOnlyList<Recipe>> GetListAsync(string? search, RecipeCategory? category, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Recipe>>([]);
        public Task UpdateAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;
    }
}
