using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class RecipeOwnershipIsolationTests
{
    [Fact]
    public async Task GetRecipeById_ReturnsNull_ForOtherUsersRecipe()
    {
        var recipeId = Guid.NewGuid();
        var repo = new OwnershipRecipeRepository(
        [
            new Recipe
            {
                Id = recipeId,
                OwnerUserId = "user-a",
                Title = new RecipeTitle("Private"),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
        ]);

        var sut = new GetRecipeByIdQueryHandler(repo, new FixedCurrentUser("user-b"));
        var result = await sut.HandleAsync(new GetRecipeByIdQuery { RecipeId = recipeId });

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRecipeList_ReturnsOnlyCurrentUsersRecipes()
    {
        var repo = new OwnershipRecipeRepository(
        [
            new Recipe
            {
                Id = Guid.NewGuid(),
                OwnerUserId = "user-a",
                Title = new RecipeTitle("A"),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            new Recipe
            {
                Id = Guid.NewGuid(),
                OwnerUserId = "user-b",
                Title = new RecipeTitle("B"),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
        ]);

        var sut = new GetRecipeListQueryHandler(repo, new FixedCurrentUser("user-a"));
        var result = await sut.HandleAsync(new GetRecipeListQuery());

        Assert.Single(result.Items);
        Assert.Equal("A", result.Items[0].Title);
    }

    private sealed class OwnershipRecipeRepository(IReadOnlyList<Recipe> recipes) : IRecipeRepository
    {
        public Task AddAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(string ownerUserId, Guid id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Recipe?> GetByIdAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(recipes.FirstOrDefault(r => r.Id == id && r.OwnerUserId == ownerUserId));

        public Task<Recipe?> GetByIdForUpdateAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            GetByIdAsync(ownerUserId, id, ct);

        public Task<IReadOnlyList<Recipe>> GetByIdsAsync(
            string ownerUserId,
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recipe>>(
                recipes.Where(r => r.OwnerUserId == ownerUserId && ids.Contains(r.Id)).ToList());

        public Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(
            string ownerUserId,
            Guid recipeId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<Recipe>> GetListAsync(
            string ownerUserId,
            string? search,
            RecipeCategory? category,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recipe>>(
                recipes.Where(r => r.OwnerUserId == ownerUserId).ToList());

        public Task<bool> IsRecipeImageAccessibleAsync(
            string ownerUserId,
            string fileName,
            CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task UpdateAsync(string ownerUserId, Recipe recipe, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
