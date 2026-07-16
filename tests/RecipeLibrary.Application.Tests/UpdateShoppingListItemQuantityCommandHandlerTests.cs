using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class UpdateShoppingListItemQuantityCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_Throws_WhenQuantityInvalid()
    {
        var itemId = Guid.NewGuid();
        var repo = new FakeShoppingListRepository
        {
            Item = new ShoppingListItem
            {
                Id = itemId,
                Unit = Unit.Gram,
                Quantity = new Quantity(2),
            },
        };
        var sut = new UpdateShoppingListItemQuantityCommandHandler(repo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new UpdateShoppingListItemQuantityCommand { ItemId = itemId, Quantity = 0 }));
    }

    [Fact]
    public async Task HandleAsync_UpdatesQuantity_WhenValid()
    {
        var itemId = Guid.NewGuid();
        var repo = new FakeShoppingListRepository
        {
            Item = new ShoppingListItem
            {
                Id = itemId,
                Unit = Unit.Gram,
                Quantity = new Quantity(2),
            },
            UpdateQuantityResult = true,
        };
        var sut = new UpdateShoppingListItemQuantityCommandHandler(repo);

        var result = await sut.HandleAsync(new UpdateShoppingListItemQuantityCommand { ItemId = itemId, Quantity = 5 });

        Assert.True(result.Updated);
        Assert.Equal(5, result.Quantity);
        Assert.Equal(itemId, repo.LastUpdatedItemId);
        Assert.Equal(5, repo.LastUpdatedQuantity);
    }

    private sealed class FakeShoppingListRepository : IShoppingListRepository
    {
        public ShoppingListItem? Item { get; init; }
        public bool UpdateQuantityResult { get; init; }
        public Guid? LastUpdatedItemId { get; private set; }
        public decimal? LastUpdatedQuantity { get; private set; }

        public Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default) =>
            Task.FromResult(Item?.Id == itemId ? Item : null);

        public Task<bool> UpdateItemQuantityAsync(Guid itemId, decimal quantity, CancellationToken ct = default)
        {
            LastUpdatedItemId = itemId;
            LastUpdatedQuantity = quantity;
            return Task.FromResult(UpdateQuantityResult);
        }

        public Task<ShoppingListGroup?> GetGroupWithListsAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<ShoppingListGroup?>(null);
        public Task<ShoppingListGroup?> GetGroupByOwnerUserIdAsync(string ownerUserId, CancellationToken ct = default) => Task.FromResult<ShoppingListGroup?>(null);
        public Task<bool> IsGroupAccessibleAsync(Guid groupId, string? ownerUserId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> IsListAccessibleAsync(Guid listId, string? ownerUserId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ShoppingListGroup> CreateGroupWithPrimaryListAsync(string primaryListName, string? ownerUserId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default) => Task.FromResult<ShoppingList?>(null);
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
        public Task<bool> RemoveItemAsync(Guid itemId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
