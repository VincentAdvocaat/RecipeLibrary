using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class GetNextShoppingListNameQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsFirstName_WhenNoListsExist()
    {
        var repo = new NamesOnlyFakeShoppingListRepository([]);
        var sut = new GetNextShoppingListNameQueryHandler(repo);

        var result = await sut.HandleAsync(new GetNextShoppingListNameQuery { NameFormat = "Shop {0}" });

        Assert.Equal("Shop 1", result.Name);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNextNumber_WhenNamesExist()
    {
        var repo = new NamesOnlyFakeShoppingListRepository(["Shop 1", "Shop 2"]);
        var sut = new GetNextShoppingListNameQueryHandler(repo);

        var result = await sut.HandleAsync(new GetNextShoppingListNameQuery { NameFormat = "Shop {0}" });

        Assert.Equal("Shop 3", result.Name);
    }

    private sealed class NamesOnlyFakeShoppingListRepository(IReadOnlyList<string> names) : IShoppingListRepository
    {
        public Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default) =>
            Task.FromResult(names);

        public Task<ShoppingListGroup?> GetGroupWithListsAsync(Guid groupId, CancellationToken ct = default) =>
            Task.FromResult<ShoppingListGroup?>(null);

        public Task<ShoppingListGroup> CreateGroupWithPrimaryListAsync(string primaryListName, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default) =>
            Task.FromResult<ShoppingList?>(null);

        public Task<ShoppingList?> GetPrimaryListInGroupAsync(Guid groupId, CancellationToken ct = default) =>
            Task.FromResult<ShoppingList?>(null);

        public Task<bool> GroupHasSecondListAsync(Guid groupId, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<int> GetUncheckedItemCountForGroupAsync(Guid groupId, CancellationToken ct = default) =>
            Task.FromResult(0);

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
