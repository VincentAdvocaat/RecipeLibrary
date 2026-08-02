using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class AddRecipesToShoppingListCommandHandlerTests
{
    private const string TestUserId = "test-user";

    [Fact]
    public async Task HandleAsync_Throws_WhenNoRecipeIds()
    {
        var sut = CreateSut(
            new RecordingShoppingListRepository(),
            new FakeRecipeRepository(new Recipe
            {
                Id = Guid.NewGuid(),
                OwnerUserId = TestUserId,
                Title = new RecipeTitle("X"),
            }));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new AddRecipesToShoppingListCommand { ShoppingListId = Guid.NewGuid(), RecipeIds = [] }));
    }

    [Fact]
    public async Task HandleAsync_MergesIngredientsIntoList()
    {
        var listId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var list = new ShoppingList { Id = listId, GroupId = groupId, Items = [] };
        var recipe = new Recipe
        {
            Id = recipeId,
            OwnerUserId = TestUserId,
            Title = new RecipeTitle("Lasagna"),
            Ingredients =
            [
                new Ingredient
                {
                    Id = Guid.NewGuid(),
                    IngredientId = Guid.NewGuid(),
                    Name = "Gehakt",
                    Quantity = new Quantity(500),
                    Unit = Unit.Gram,
                },
            ],
        };

        var shoppingRepo = new RecordingShoppingListRepository { List = list };
        var recipeRepo = new FakeRecipeRepository(recipe);
        var sut = CreateSut(shoppingRepo, recipeRepo);

        var result = await sut.HandleAsync(new AddRecipesToShoppingListCommand
        {
            ShoppingListId = listId,
            RecipeIds = [recipeId],
        });

        Assert.Equal(1, result.RecipesAdded);
        Assert.Equal(1, result.IngredientsAdded);
        Assert.NotNull(shoppingRepo.ReplacedItems);
        Assert.Single(shoppingRepo.ReplacedItems!);
    }

    [Fact]
    public async Task HandleAsync_ExcludesPantryStaples_FromMergedList()
    {
        var listId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var saltId = Guid.NewGuid();
        var list = new ShoppingList { Id = listId, GroupId = groupId, Items = [] };
        var recipe = new Recipe
        {
            Id = recipeId,
            OwnerUserId = TestUserId,
            Title = new RecipeTitle("Soep"),
            Ingredients =
            [
                new Ingredient
                {
                    Id = Guid.NewGuid(),
                    IngredientId = saltId,
                    Name = "Zout",
                    Quantity = new Quantity(1),
                    Unit = Unit.Teaspoon,
                },
                new Ingredient
                {
                    Id = Guid.NewGuid(),
                    IngredientId = Guid.NewGuid(),
                    Name = "Ui",
                    Quantity = new Quantity(1),
                    Unit = Unit.Piece,
                },
            ],
        };

        var shoppingRepo = new RecordingShoppingListRepository { List = list };
        var recipeRepo = new FakeRecipeRepository(recipe);
        var pantryRepo = new RecordingPantryRepository
        {
            Items =
            [
                new PantryItem
                {
                    Id = Guid.NewGuid(),
                    OwnerUserId = TestUserId,
                    DisplayName = "Zout",
                    CanonicalIngredientId = saltId,
                },
            ],
        };
        var sut = CreateSut(shoppingRepo, recipeRepo, pantryRepo);

        var result = await sut.HandleAsync(new AddRecipesToShoppingListCommand
        {
            ShoppingListId = listId,
            RecipeIds = [recipeId],
        });

        Assert.Equal(1, result.IngredientsAdded);
        Assert.NotNull(shoppingRepo.ReplacedItems);
        Assert.Single(shoppingRepo.ReplacedItems!);
        Assert.Equal("Ui", shoppingRepo.ReplacedItems![0].DisplayName);
    }

    private static AddRecipesToShoppingListCommandHandler CreateSut(
        RecordingShoppingListRepository shoppingRepo,
        FakeRecipeRepository recipeRepo,
        RecordingPantryRepository? pantryRepo = null) =>
        new(
            recipeRepo,
            shoppingRepo,
            pantryRepo ?? new RecordingPantryRepository(),
            new FixedCurrentUser(TestUserId),
            new ShoppingListIngredientMerger(new IngredientTextNormalizer()),
            new PantryExclusionFilter(new PantryIngredientMerger(new IngredientTextNormalizer())),
            new NoOpUnitOfWork());

    private sealed class FakeRecipeRepository(Recipe recipe) : IRecipeRepository
    {
        public Task<IReadOnlyList<Recipe>> GetByIdsAsync(
            string ownerUserId,
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recipe>>(ids.Contains(recipe.Id) ? [recipe] : []);

        public Task AddAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(string ownerUserId, Guid id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Recipe?> GetByIdAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Recipe?>(null);

        public Task<Recipe?> GetByIdForUpdateAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Recipe?>(null);

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
