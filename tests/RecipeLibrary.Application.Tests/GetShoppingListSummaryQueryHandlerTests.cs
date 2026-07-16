using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class GetShoppingListSummaryQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsUncheckedCount_FromRepository()
    {
        var groupId = Guid.NewGuid();
        var repo = new FakeShoppingListRepository(uncheckedCount: 3);
        var sut = new GetShoppingListSummaryQueryHandler(repo, new AnonymousUserContext());

        var result = await sut.HandleAsync(new GetShoppingListSummaryQuery { GroupId = groupId });

        Assert.Equal(3, result.UncheckedItemCount);
        Assert.Equal(groupId, repo.LastGroupId);
    }

    private sealed class AnonymousUserContext : IShoppingListUserContext
    {
        public string? OwnerUserId => null;
    }

    private sealed class FakeShoppingListRepository(int uncheckedCount) : IShoppingListRepository
    {
        public Guid? LastGroupId { get; private set; }

        public Task<int> GetUncheckedItemCountForGroupAsync(Guid groupId, CancellationToken ct = default)
        {
            LastGroupId = groupId;
            return Task.FromResult(uncheckedCount);
        }

        public Task<ShoppingListGroup?> GetGroupWithListsAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<ShoppingListGroup?>(null);
        public Task<ShoppingListGroup> CreateGroupWithPrimaryListAsync(string primaryListName, string? ownerUserId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default) => Task.FromResult<ShoppingList?>(null);
        public Task<ShoppingList?> GetPrimaryListInGroupAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<ShoppingList?>(null);
        public Task<bool> GroupHasSecondListAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearListItemsAsync(Guid shoppingListId, CancellationToken ct = default) => Task.CompletedTask;
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
