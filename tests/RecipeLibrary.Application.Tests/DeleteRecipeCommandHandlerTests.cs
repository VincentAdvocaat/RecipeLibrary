using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class DeleteRecipeCommandHandlerTests
{
    private const string TestUserId = "test-user";

    [Fact]
    public async Task HandleAsync_ReturnsFalse_WhenRecipeDoesNotExist()
    {
        var repo = new FakeRecipeRepository(exists: false);
        var sut = new DeleteRecipeCommandHandler(repo, new FixedCurrentUser(TestUserId));

        var result = await sut.HandleAsync(new DeleteRecipeCommand { RecipeId = Guid.NewGuid() });

        Assert.False(result.Deleted);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTrue_WhenRecipeWasDeleted()
    {
        var recipeId = Guid.NewGuid();
        var repo = new FakeRecipeRepository(exists: true, recipeId: recipeId);
        var sut = new DeleteRecipeCommandHandler(repo, new FixedCurrentUser(TestUserId));

        var result = await sut.HandleAsync(new DeleteRecipeCommand { RecipeId = recipeId });

        Assert.True(result.Deleted);
        Assert.True(repo.DeleteWasCalled);
    }

    private sealed class FakeRecipeRepository(bool exists, Guid? recipeId = null) : IRecipeRepository
    {
        private readonly Guid _id = recipeId ?? Guid.NewGuid();
        private bool _exists = exists;
        public bool DeleteWasCalled { get; private set; }

        public Task AddAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(string ownerUserId, Guid id, CancellationToken ct = default)
        {
            DeleteWasCalled = true;
            if (id == _id)
            {
                _exists = false;
            }

            return Task.CompletedTask;
        }

        public Task<Recipe?> GetByIdAsync(string ownerUserId, Guid id, CancellationToken ct = default)
        {
            if (!_exists || id != _id)
            {
                return Task.FromResult<Recipe?>(null);
            }

            return Task.FromResult<Recipe?>(new Recipe
            {
                Id = _id,
                OwnerUserId = TestUserId,
                Title = new RecipeTitle("Test"),
            });
        }

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
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recipe>>([]);

        public Task UpdateAsync(string ownerUserId, Recipe recipe, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> IsRecipeImageAccessibleAsync(
            string ownerUserId,
            string fileName,
            CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
