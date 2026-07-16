using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class MoveShoppingListItemToPantryCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpsertsPantryAndRemovesShoppingListItem()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var shoppingRepo = new FakeShoppingListRepository
        {
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Zout",
                Quantity = new Quantity(1),
                Unit = Unit.Teaspoon,
            },
            List = new ShoppingList { Id = listId, GroupId = groupId },
            RemoveResult = true,
        };
        var pantryRepo = new FakePantryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var sut = new MoveShoppingListItemToPantryCommandHandler(
            shoppingRepo,
            pantryRepo,
            new AnonymousShoppingListUserContext(),
            unitOfWork,
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        var result = await sut.HandleAsync(new MoveShoppingListItemToPantryCommand { ItemId = itemId });

        Assert.True(result.Moved);
        Assert.True(unitOfWork.Executed);
        Assert.NotNull(pantryRepo.UpsertedItem);
        Assert.Equal("Zout", pantryRepo.UpsertedItem!.DisplayName);
        Assert.Equal($"group:{groupId:D}", pantryRepo.UpsertedItem.OwnerUserId);
        Assert.Equal(itemId, shoppingRepo.LastRemovedItemId);
    }

    [Fact]
    public async Task HandleAsync_IsIdempotent_WhenAlreadyInPantry()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var existingPantryId = Guid.NewGuid();
        var ownerKey = $"group:{groupId:D}";
        var shoppingRepo = new FakeShoppingListRepository
        {
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Zout",
                Quantity = new Quantity(1),
                Unit = Unit.Teaspoon,
            },
            List = new ShoppingList { Id = listId, GroupId = groupId },
            RemoveResult = true,
        };
        var pantryRepo = new FakePantryRepository
        {
            Items =
            [
                new PantryItem
                {
                    Id = existingPantryId,
                    OwnerUserId = ownerKey,
                    DisplayName = "Zout",
                },
            ],
        };
        var sut = new MoveShoppingListItemToPantryCommandHandler(
            shoppingRepo,
            pantryRepo,
            new AnonymousShoppingListUserContext(),
            new FakeUnitOfWork(),
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        var result = await sut.HandleAsync(new MoveShoppingListItemToPantryCommand { ItemId = itemId });

        Assert.True(result.Moved);
        Assert.Equal(existingPantryId, result.PantryItemId);
        Assert.Equal(itemId, shoppingRepo.LastRemovedItemId);
    }

    [Fact]
    public async Task HandleAsync_UsesAuthenticatedOwnerUserId_ForPantryKey()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var shoppingRepo = new FakeShoppingListRepository
        {
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Olie",
                Quantity = new Quantity(1),
                Unit = Unit.Tablespoon,
            },
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid() },
            RemoveResult = true,
        };
        var pantryRepo = new FakePantryRepository();
        var sut = new MoveShoppingListItemToPantryCommandHandler(
            shoppingRepo,
            pantryRepo,
            new FixedShoppingListUserContext("user-42"),
            new FakeUnitOfWork(),
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        await sut.HandleAsync(new MoveShoppingListItemToPantryCommand { ItemId = itemId });

        Assert.Equal("user-42", pantryRepo.UpsertedItem!.OwnerUserId);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool Executed { get; private set; }

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
        {
            Executed = true;
            await action(ct);
        }
    }

    private sealed class FakeShoppingListRepository : IShoppingListRepository
    {
        public ShoppingListItem? Item { get; init; }
        public ShoppingList? List { get; init; }
        public bool RemoveResult { get; init; }
        public Guid? LastRemovedItemId { get; private set; }

        public Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default) =>
            Task.FromResult(Item?.Id == itemId ? Item : null);

        public Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default) =>
            Task.FromResult(List?.Id == listId ? List : null);

        public Task<bool> RemoveItemAsync(Guid itemId, CancellationToken ct = default)
        {
            LastRemovedItemId = itemId;
            return Task.FromResult(RemoveResult);
        }

        public Task<bool> IsListAccessibleAsync(Guid listId, string? ownerUserId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ShoppingListGroup?> GetGroupWithListsAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<ShoppingListGroup?>(null);
        public Task<ShoppingListGroup?> GetGroupByOwnerUserIdAsync(string ownerUserId, CancellationToken ct = default) => Task.FromResult<ShoppingListGroup?>(null);
        public Task<bool> IsGroupAccessibleAsync(Guid groupId, string? ownerUserId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ShoppingListGroup> CreateGroupWithPrimaryListAsync(string primaryListName, string? ownerUserId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ShoppingList?> GetPrimaryListInGroupAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<ShoppingList?>(null);
        public Task<bool> GroupHasSecondListAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> GetUncheckedItemCountForGroupAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult(0);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearListItemsAsync(Guid shoppingListId, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteListAsync(Guid shoppingListId, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteGroupAsync(Guid groupId, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplaceListItemsAsync(Guid shoppingListId, IReadOnlyList<ShoppingListItem> items, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ShoppingList> AddListToGroupAsync(Guid groupId, string name, int storeOrder, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> ToggleItemCheckedAsync(Guid itemId, bool isChecked, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> UpdateItemQuantityAsync(Guid itemId, decimal quantity, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class FakePantryRepository : IPantryRepository
    {
        public IReadOnlyList<PantryItem> Items { get; init; } = [];
        public PantryItem? UpsertedItem { get; private set; }

        public Task<IReadOnlyList<PantryItem>> GetByOwnerKeyAsync(string ownerKey, CancellationToken ct = default) =>
            Task.FromResult(Items);

        public Task<PantryItem?> GetByIdForOwnerAsync(Guid itemId, string ownerKey, CancellationToken ct = default) =>
            Task.FromResult<PantryItem?>(null);

        public Task<PantryItem> UpsertAsync(PantryItem item, CancellationToken ct = default)
        {
            UpsertedItem = item;
            return Task.FromResult(item);
        }

        public Task<bool> RemoveAsync(Guid itemId, string ownerKey, CancellationToken ct = default) =>
            Task.FromResult(false);
    }

    private sealed class FixedShoppingListUserContext(string ownerUserId) : IShoppingListUserContext
    {
        public string? OwnerUserId => ownerUserId;
    }
}
