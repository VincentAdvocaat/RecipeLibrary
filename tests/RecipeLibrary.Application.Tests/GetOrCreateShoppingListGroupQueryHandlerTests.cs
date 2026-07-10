using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class GetOrCreateShoppingListGroupQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsExistingGroup_WhenGroupIdProvided()
    {
        var groupId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var group = new ShoppingListGroup
        {
            Id = groupId,
            Lists =
            [
                new ShoppingList { Id = listId, GroupId = groupId, Name = "List 1", StoreOrder = 1 },
            ],
        };

        var repo = new FakeShoppingListRepository { ExistingGroup = group };
        var sut = new GetOrCreateShoppingListGroupQueryHandler(repo);

        var result = await sut.HandleAsync(new GetOrCreateShoppingListGroupQuery
        {
            GroupId = groupId,
            DefaultListNameFormat = "List {0}",
        });

        Assert.Equal(groupId, result.GroupId);
        Assert.Single(result.Lists);
        Assert.Equal("List 1", result.Lists[0].Name);
    }

    [Fact]
    public async Task HandleAsync_CreatesGroup_WhenNoGroupExists()
    {
        var createdGroupId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var repo = new FakeShoppingListRepository
        {
            CreatedGroup = new ShoppingListGroup
            {
                Id = createdGroupId,
                Lists = [new ShoppingList { Id = listId, GroupId = createdGroupId, Name = "List 1", StoreOrder = 1 }],
            },
        };

        var sut = new GetOrCreateShoppingListGroupQueryHandler(repo);

        var result = await sut.HandleAsync(new GetOrCreateShoppingListGroupQuery { DefaultListNameFormat = "List {0}" });

        Assert.Equal(createdGroupId, result.GroupId);
        Assert.True(repo.CreateGroupCalled);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenNameFormatMissing()
    {
        var sut = new GetOrCreateShoppingListGroupQueryHandler(new FakeShoppingListRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new GetOrCreateShoppingListGroupQuery { DefaultListNameFormat = "  " }));
    }

    private sealed class FakeShoppingListRepository : IShoppingListRepository
    {
        public ShoppingListGroup? ExistingGroup { get; init; }
        public ShoppingListGroup? CreatedGroup { get; init; }
        public bool CreateGroupCalled { get; private set; }

        public Task<ShoppingListGroup?> GetGroupWithListsAsync(Guid groupId, CancellationToken ct = default)
        {
            if (ExistingGroup is not null && ExistingGroup.Id == groupId)
            {
                return Task.FromResult<ShoppingListGroup?>(ExistingGroup);
            }

            if (CreatedGroup is not null && CreatedGroup.Id == groupId)
            {
                return Task.FromResult<ShoppingListGroup?>(CreatedGroup);
            }

            return Task.FromResult<ShoppingListGroup?>(null);
        }

        public Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<ShoppingListGroup> CreateGroupWithPrimaryListAsync(string primaryListName, string? ownerUserId = null, CancellationToken ct = default)
        {
            CreateGroupCalled = true;
            return Task.FromResult(CreatedGroup ?? new ShoppingListGroup { Id = Guid.NewGuid() });
        }

        public Task<ShoppingListGroup?> GetGroupByOwnerUserIdAsync(string ownerUserId, CancellationToken ct = default) => Task.FromResult<ShoppingListGroup?>(null);
        public Task<bool> IsGroupAccessibleAsync(Guid groupId, string? ownerUserId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> IsListAccessibleAsync(Guid listId, string? ownerUserId, CancellationToken ct = default) => Task.FromResult(true);
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
        public Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default) => Task.FromResult(false);
    }
}
