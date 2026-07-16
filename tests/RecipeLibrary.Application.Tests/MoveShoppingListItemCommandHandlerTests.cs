using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class MoveShoppingListItemCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_MovesItemBetweenListsInSameGroup()
    {
        var groupId = Guid.NewGuid();
        var sourceListId = Guid.NewGuid();
        var targetListId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var item = new ShoppingListItem
        {
            Id = itemId,
            ShoppingListId = sourceListId,
            DisplayName = "Gehakt",
            Quantity = new Quantity(500),
            Unit = Unit.Gram,
        };

        var sourceList = new ShoppingList { Id = sourceListId, GroupId = groupId, Items = [item] };
        var targetList = new ShoppingList { Id = targetListId, GroupId = groupId, Items = [] };
        var repo = new FakeShoppingListRepository(item, sourceList, targetList);
        var sut = new MoveShoppingListItemCommandHandler(repo, new AnonymousUserContext(), new ShoppingListIngredientMerger(new IngredientTextNormalizer()));

        var result = await sut.HandleAsync(new MoveShoppingListItemCommand
        {
            ItemId = itemId,
            TargetShoppingListId = targetListId,
        });

        Assert.True(result.Moved);
        Assert.NotNull(repo.SourceItems);
        Assert.Empty(repo.SourceItems!);
        Assert.NotNull(repo.TargetItems);
        Assert.Single(repo.TargetItems!);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTrue_WhenItemAlreadyOnTargetList()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var item = new ShoppingListItem { Id = itemId, ShoppingListId = listId, DisplayName = "Gehakt" };
        var list = new ShoppingList { Id = listId, GroupId = Guid.NewGuid(), Items = [item] };
        var repo = new FakeShoppingListRepository(item, list, list);
        var sut = new MoveShoppingListItemCommandHandler(repo, new AnonymousUserContext(), new ShoppingListIngredientMerger(new IngredientTextNormalizer()));

        var result = await sut.HandleAsync(new MoveShoppingListItemCommand { ItemId = itemId, TargetShoppingListId = listId });

        Assert.True(result.Moved);
        Assert.Null(repo.SourceItems);
    }

    private sealed class AnonymousUserContext : IShoppingListUserContext
    {
        public string? OwnerUserId => null;
    }

    private sealed class FakeShoppingListRepository(
        ShoppingListItem item,
        ShoppingList sourceList,
        ShoppingList targetList) : IShoppingListRepository
    {
        public IReadOnlyList<ShoppingListItem>? SourceItems { get; private set; }
        public IReadOnlyList<ShoppingListItem>? TargetItems { get; private set; }

        public Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default) =>
            Task.FromResult<ShoppingListItem?>(itemId == item.Id ? item : null);

        public Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default)
        {
            if (listId == sourceList.Id)
            {
                return Task.FromResult<ShoppingList?>(sourceList);
            }

            if (listId == targetList.Id)
            {
                return Task.FromResult<ShoppingList?>(targetList);
            }

            return Task.FromResult<ShoppingList?>(null);
        }

        public Task ReplaceListItemsAsync(Guid shoppingListId, IReadOnlyList<ShoppingListItem> items, CancellationToken ct = default)
        {
            if (shoppingListId == sourceList.Id)
            {
                SourceItems = items;
            }
            else if (shoppingListId == targetList.Id)
            {
                TargetItems = items;
            }

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
        public Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
