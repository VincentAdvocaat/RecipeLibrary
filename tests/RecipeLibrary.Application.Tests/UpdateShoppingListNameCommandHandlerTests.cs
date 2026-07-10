using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class UpdateShoppingListNameCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_Throws_WhenNameEmpty()
    {
        var sut = new UpdateShoppingListNameCommandHandler(new FakeShoppingListRepository(), new AnonymousShoppingListUserContext());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new UpdateShoppingListNameCommand { ShoppingListId = Guid.NewGuid(), Name = "  " }));
    }

    [Fact]
    public async Task HandleAsync_ReturnsUpdated_WhenRepositorySucceeds()
    {
        var listId = Guid.NewGuid();
        var repo = new FakeShoppingListRepository { UpdateNameResult = true };
        var sut = new UpdateShoppingListNameCommandHandler(repo, new AnonymousShoppingListUserContext());

        var result = await sut.HandleAsync(new UpdateShoppingListNameCommand { ShoppingListId = listId, Name = "Store 2" });

        Assert.True(result.Updated);
        Assert.Equal(listId, repo.LastUpdatedListId);
        Assert.Equal("Store 2", repo.LastUpdatedName);
    }

    private sealed class FakeShoppingListRepository : IShoppingListRepository
    {
        public bool UpdateNameResult { get; init; }
        public Guid? LastUpdatedListId { get; private set; }
        public string? LastUpdatedName { get; private set; }

        public Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default)
        {
            LastUpdatedListId = shoppingListId;
            LastUpdatedName = name;
            return Task.FromResult(UpdateNameResult);
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
        public Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default) => Task.FromResult<ShoppingListItem?>(null);
        public Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
