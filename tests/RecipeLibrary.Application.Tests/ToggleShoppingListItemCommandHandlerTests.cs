using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class ToggleShoppingListItemCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsCheckedState_WhenRepositoryUpdates()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var repo = new FakeShoppingListRepository
        {
            ToggleResult = true,
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Melk",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
                IsChecked = false,
            },
            List = new ShoppingList { Id = listId, GroupId = groupId },
        };
        var pantryRepo = new FakePantryRepository();
        var sut = new ToggleShoppingListItemCommandHandler(
            repo,
            pantryRepo,
            new AnonymousShoppingListUserContext(),
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        var result = await sut.HandleAsync(new ToggleShoppingListItemCommand { ItemId = itemId, IsChecked = true });

        Assert.True(result.IsChecked);
        Assert.Equal(itemId, repo.LastToggledItemId);
        Assert.NotNull(pantryRepo.UpsertedItem);
        Assert.Equal("Melk", pantryRepo.UpsertedItem!.DisplayName);
    }

    private sealed class FakeShoppingListRepository : IShoppingListRepository
    {
        public bool ToggleResult { get; init; }
        public ShoppingListItem? Item { get; init; }
        public ShoppingList? List { get; init; }
        public Guid? LastToggledItemId { get; private set; }

        public Task<bool> ToggleItemCheckedAsync(Guid itemId, bool isChecked, CancellationToken ct = default)
        {
            LastToggledItemId = itemId;
            return Task.FromResult(ToggleResult);
        }

        public Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default) =>
            Task.FromResult(Item?.Id == itemId ? Item : null);

        public Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default) =>
            Task.FromResult(List?.Id == listId ? List : null);

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
        public Task ReplaceListItemsAsync(Guid shoppingListId, IReadOnlyList<ShoppingListItem> items, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ShoppingList> AddListToGroupAsync(Guid groupId, string name, int storeOrder, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> RemoveItemAsync(Guid itemId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> UpdateItemQuantityAsync(Guid itemId, decimal quantity, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class FakePantryRepository : IPantryRepository
    {
        public PantryItem? UpsertedItem { get; private set; }

        public Task<IReadOnlyList<PantryItem>> GetByOwnerKeyAsync(string ownerKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PantryItem>>([]);

        public Task<PantryItem?> GetByIdForOwnerAsync(Guid itemId, string ownerKey, CancellationToken ct = default) =>
            Task.FromResult<PantryItem?>(null);

        public Task<PantryItem> UpsertAsync(PantryItem item, CancellationToken ct = default)
        {
            UpsertedItem = item;
            return Task.FromResult(item);
        }

        public Task<bool> UpdateQuantityAsync(Guid itemId, string ownerKey, decimal quantity, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> RemoveAsync(Guid itemId, string ownerKey, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
