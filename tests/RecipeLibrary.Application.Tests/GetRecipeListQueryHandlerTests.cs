using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class GetRecipeListQueryHandlerTests
{
    private const string TestUserId = "test-user";

    [Fact]
    public async Task HandleAsync_ForwardsSearchAndMappedCategory_ToRepository()
    {
        var repo = new FakeRecipeRepository();
        var sut = new GetRecipeListQueryHandler(repo, new FixedCurrentUser(TestUserId));

        await sut.HandleAsync(new GetRecipeListQuery
        {
            Search = "Lasagna",
            Category = (int)RecipeCategory.Meat,
        });

        Assert.Equal(TestUserId, repo.LastOwnerUserId);
        Assert.Equal("Lasagna", repo.LastSearch);
        Assert.Equal(RecipeCategory.Meat, repo.LastCategory);
    }

    [Fact]
    public async Task HandleAsync_ForwardsNullCategory_WhenUndefined()
    {
        var repo = new FakeRecipeRepository();
        var sut = new GetRecipeListQueryHandler(repo, new FixedCurrentUser(TestUserId));

        await sut.HandleAsync(new GetRecipeListQuery
        {
            Search = null,
            Category = 999,
        });

        Assert.Equal(TestUserId, repo.LastOwnerUserId);
        Assert.Null(repo.LastSearch);
        Assert.Null(repo.LastCategory);
    }

    private sealed class FakeRecipeRepository : IRecipeRepository
    {
        public string? LastOwnerUserId { get; private set; }
        public string? LastSearch { get; private set; }
        public RecipeCategory? LastCategory { get; private set; }

        public Task AddAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(string ownerUserId, Guid id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Recipe?> GetByIdAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Recipe?>(null);

        public Task<Recipe?> GetByIdForUpdateAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            GetByIdAsync(ownerUserId, id, ct);

        public Task<IReadOnlyList<Recipe>> GetByIdsAsync(
            string ownerUserId,
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recipe>>([]);

        public Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(
            string ownerUserId,
            Guid recipeId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<Recipe>> GetListAsync(
            string ownerUserId,
            string? search,
            RecipeCategory? category,
            CancellationToken ct = default)
        {
            LastOwnerUserId = ownerUserId;
            LastSearch = search;
            LastCategory = category;
            return Task.FromResult<IReadOnlyList<Recipe>>([]);
        }

        public Task UpdateAsync(string ownerUserId, Recipe recipe, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> IsRecipeImageAccessibleAsync(
            string ownerUserId,
            string fileName,
            CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
