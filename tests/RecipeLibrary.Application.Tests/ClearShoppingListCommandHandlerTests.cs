using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class ClearShoppingListCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsFalse_WhenListDoesNotExist()
    {
        var listId = Guid.NewGuid();
        var repo = new FakeShoppingListRepository(list: null);
        var sut = new ClearShoppingListCommandHandler(repo, new AnonymousUserContext());

        var result = await sut.HandleAsync(new ClearShoppingListCommand { ShoppingListId = listId });

        Assert.False(result.Cleared);
        Assert.False(repo.ClearWasCalled);
    }

    [Fact]
    public async Task HandleAsync_ClearsItems_WhenListExists()
    {
        var listId = Guid.NewGuid();
        var list = new ShoppingList { Id = listId, GroupId = Guid.NewGuid(), Name = "Main" };
        var repo = new FakeShoppingListRepository(list);
        var sut = new ClearShoppingListCommandHandler(repo, new AnonymousUserContext());

        var result = await sut.HandleAsync(new ClearShoppingListCommand { ShoppingListId = listId });

        Assert.True(result.Cleared);
        Assert.True(repo.ClearWasCalled);
        Assert.Equal(listId, repo.LastClearedListId);
    }

    private sealed class AnonymousUserContext : IShoppingListUserContext
    {
        public string? OwnerUserId => null;
    }

    private sealed class FakeShoppingListRepository(ShoppingList? list) : IShoppingListRepository
    {
        public bool ClearWasCalled { get; private set; }
        public Guid? LastClearedListId { get; private set; }

        public Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default) =>
            Task.FromResult(list is not null && list.Id == listId ? list : null);

        public Task ClearListItemsAsync(Guid shoppingListId, CancellationToken ct = default)
        {
            ClearWasCalled = true;
            LastClearedListId = shoppingListId;
            return Task.CompletedTask;
        }

        public Task<ShoppingListGroup?> GetGroupWithListsAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<ShoppingListGroup?>(null);
        public Task<ShoppingListGroup> CreateGroupWithPrimaryListAsync(string primaryListName, string? ownerUserId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ShoppingList?> GetPrimaryListInGroupAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<ShoppingList?>(null);
        public Task<bool> GroupHasSecondListAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> GetUncheckedItemCountForGroupAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult(0);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteListAsync(Guid shoppingListId, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteGroupAsync(Guid groupId, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplaceListItemsAsync(Guid shoppingListId, IReadOnlyList<ShoppingListItem> items, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ShoppingList> AddListToGroupAsync(Guid groupId, string name, int storeOrder, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> RemoveItemAsync(Guid itemId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ToggleItemCheckedAsync(Guid itemId, bool isChecked, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> UpdateItemQuantityAsync(Guid itemId, decimal quantity, CancellationToken ct = default) => Task.FromResult(false);
        public Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default) => Task.FromResult<ShoppingListItem?>(null);
        public Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> IsGroupAccessibleAsync(Guid groupId, string? ownerUserId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> IsListAccessibleAsync(Guid listId, string? ownerUserId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ShoppingListGroup?> GetGroupByOwnerUserIdAsync(string ownerUserId, CancellationToken ct = default) => Task.FromResult<ShoppingListGroup?>(null);
    }
}
