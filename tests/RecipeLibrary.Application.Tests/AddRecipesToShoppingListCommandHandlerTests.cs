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
            new FakeShoppingListRepository(),
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

        var shoppingRepo = new FakeShoppingListRepository { List = list };
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

        var shoppingRepo = new FakeShoppingListRepository { List = list };
        var recipeRepo = new FakeRecipeRepository(recipe);
        var pantryRepo = new FakePantryRepository
        {
            Items =
            [
                new PantryItem
                {
                    Id = Guid.NewGuid(),
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

    [Fact]
    public async Task HandleAsync_Throws_WhenForeignRecipeIdIncluded()
    {
        var listId = Guid.NewGuid();
        var ownRecipeId = Guid.NewGuid();
        var foreignRecipeId = Guid.NewGuid();
        var list = new ShoppingList { Id = listId, GroupId = Guid.NewGuid(), Items = [] };
        var ownRecipe = new Recipe
        {
            Id = ownRecipeId,
            OwnerUserId = TestUserId,
            Title = new RecipeTitle("Own"),
            Ingredients =
            [
                new Ingredient
                {
                    Id = Guid.NewGuid(),
                    Name = "Ui",
                    Quantity = new Quantity(1),
                    Unit = Unit.Piece,
                },
            ],
        };

        var shoppingRepo = new FakeShoppingListRepository { List = list };
        var recipeRepo = new FakeRecipeRepository(ownRecipe);
        var sut = CreateSut(shoppingRepo, recipeRepo);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new AddRecipesToShoppingListCommand
            {
                ShoppingListId = listId,
                RecipeIds = [ownRecipeId, foreignRecipeId],
            }));

        Assert.Null(shoppingRepo.ReplacedItems);
    }

    private static AddRecipesToShoppingListCommandHandler CreateSut(
        FakeShoppingListRepository shoppingRepo,
        FakeRecipeRepository recipeRepo,
        FakePantryRepository? pantryRepo = null) =>
        new(
            recipeRepo,
            shoppingRepo,
            pantryRepo ?? new FakePantryRepository(),
            new FixedCurrentUser(TestUserId),
            new ShoppingListIngredientMerger(new IngredientTextNormalizer()),
            new PantryExclusionFilter(new PantryIngredientMerger(new IngredientTextNormalizer())));

    private sealed class FakeRecipeRepository(Recipe recipe) : IRecipeRepository
    {
        public Task<IReadOnlyList<Recipe>> GetByIdsAsync(
            string ownerUserId,
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recipe>>(
                recipe.OwnerUserId == ownerUserId && ids.Contains(recipe.Id) ? [recipe] : []);

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

    private sealed class FakeShoppingListRepository : IShoppingListRepository
    {
        public ShoppingList? List { get; init; }
        public IReadOnlyList<ShoppingListItem>? ReplacedItems { get; private set; }

        public Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default) =>
            Task.FromResult(List?.Id == listId ? List : null);

        public Task ReplaceListItemsAsync(Guid shoppingListId, IReadOnlyList<ShoppingListItem> items, CancellationToken ct = default)
        {
            ReplacedItems = items;
            return Task.CompletedTask;
        }

        public Task<ShoppingListGroup?> GetGroupWithListsAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<ShoppingListGroup?>(null);
        public Task<ShoppingListGroup?> GetGroupByOwnerUserIdAsync(string ownerUserId, CancellationToken ct = default) => Task.FromResult<ShoppingListGroup?>(null);
        public Task<bool> IsGroupAccessibleAsync(Guid groupId, string? ownerUserId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> IsListAccessibleAsync(Guid listId, string? ownerUserId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ShoppingListGroup> CreateGroupWithPrimaryListAsync(string primaryListName, string? ownerUserId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ShoppingList?> GetPrimaryListInGroupAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<ShoppingList?>(null);
        public Task<bool> GroupHasSecondListAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> GetUncheckedItemCountForGroupAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult(0);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearListItemsAsync(Guid shoppingListId, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteListAsync(Guid shoppingListId, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteGroupAsync(Guid groupId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ShoppingList> AddListToGroupAsync(Guid groupId, string name, int storeOrder, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> ToggleItemCheckedAsync(Guid itemId, bool isChecked, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> RemoveItemAsync(Guid itemId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default) => Task.FromResult<ShoppingListItem?>(null);
        public Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> UpdateItemQuantityAsync(Guid itemId, decimal quantity, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class FakePantryRepository : IPantryRepository
    {
        public IReadOnlyList<PantryItem> Items { get; init; } = [];

        public Task<IReadOnlyList<PantryItem>> GetByOwnerKeyAsync(string ownerKey, CancellationToken ct = default) =>
            Task.FromResult(Items);

        public Task<PantryItem?> GetByIdForOwnerAsync(Guid itemId, string ownerKey, CancellationToken ct = default) =>
            Task.FromResult<PantryItem?>(null);

        public Task<PantryItem> UpsertAsync(PantryItem item, CancellationToken ct = default) =>
            Task.FromResult(item);

        public Task<bool> RemoveAsync(Guid itemId, string ownerKey, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
