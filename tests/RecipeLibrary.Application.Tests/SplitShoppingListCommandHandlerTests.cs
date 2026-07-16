using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class SplitShoppingListCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_MovesSelectedItemsToNewList()
    {
        var groupId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var secondaryId = Guid.NewGuid();

        var primary = new ShoppingList
        {
            Id = primaryId,
            GroupId = groupId,
            Name = "List 1",
            StoreOrder = 1,
            Items =
            [
                new ShoppingListItem { Id = itemId, ShoppingListId = primaryId, DisplayName = "Gehakt", Quantity = new Quantity(500), Unit = Unit.Gram },
                new ShoppingListItem { Id = Guid.NewGuid(), ShoppingListId = primaryId, DisplayName = "Tomaten", Quantity = new Quantity(3), Unit = Unit.Piece },
            ],
        };

        var repo = new FakeShoppingListRepository(primary, secondaryId);
        var sut = new SplitShoppingListCommandHandler(repo, new AnonymousUserContext(), new ShoppingListIngredientMerger(new IngredientTextNormalizer()));

        var result = await sut.HandleAsync(new SplitShoppingListCommand
        {
            GroupId = groupId,
            NewListName = "Store 2",
            ItemIds = [itemId],
        });

        Assert.Equal(secondaryId, result.NewListId);
        Assert.Equal(1, result.ItemsMoved);
        Assert.NotNull(repo.PrimaryItems);
        Assert.Single(repo.PrimaryItems!);
        Assert.NotNull(repo.SecondaryItems);
        Assert.Single(repo.SecondaryItems!);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenNameEmpty()
    {
        var sut = new SplitShoppingListCommandHandler(new FakeShoppingListRepository(null!, Guid.Empty), new AnonymousUserContext(), new ShoppingListIngredientMerger(new IngredientTextNormalizer()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new SplitShoppingListCommand { GroupId = Guid.NewGuid(), NewListName = "", ItemIds = [Guid.NewGuid()] }));
    }

    private sealed class AnonymousUserContext : IShoppingListUserContext
    {
        public string? OwnerUserId => null;
    }

    private sealed class FakeShoppingListRepository(ShoppingList primary, Guid secondaryId) : IShoppingListRepository
    {
        public IReadOnlyList<ShoppingListItem>? PrimaryItems { get; private set; }
        public IReadOnlyList<ShoppingListItem>? SecondaryItems { get; private set; }

        public Task<bool> GroupHasSecondListAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult(false);

        public Task<ShoppingList?> GetPrimaryListInGroupAsync(Guid groupId, CancellationToken ct = default) =>
            Task.FromResult<ShoppingList?>(primary.GroupId == groupId ? primary : null);

        public Task<ShoppingList> AddListToGroupAsync(Guid groupId, string name, int storeOrder, CancellationToken ct = default) =>
            Task.FromResult(new ShoppingList { Id = secondaryId, GroupId = groupId, Name = name, StoreOrder = storeOrder });

        public Task ReplaceListItemsAsync(Guid shoppingListId, IReadOnlyList<ShoppingListItem> items, CancellationToken ct = default)
        {
            if (shoppingListId == primary.Id)
            {
                PrimaryItems = items;
            }
            else if (shoppingListId == secondaryId)
            {
                SecondaryItems = items;
            }

            return Task.CompletedTask;
        }

        public Task<ShoppingListGroup?> GetGroupWithListsAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<ShoppingListGroup?>(null);
        public Task<ShoppingListGroup?> GetGroupByOwnerUserIdAsync(string ownerUserId, CancellationToken ct = default) => Task.FromResult<ShoppingListGroup?>(null);
        public Task<bool> IsGroupAccessibleAsync(Guid groupId, string? ownerUserId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> IsListAccessibleAsync(Guid listId, string? ownerUserId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ShoppingListGroup> CreateGroupWithPrimaryListAsync(string primaryListName, string? ownerUserId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default) => Task.FromResult<ShoppingList?>(null);
        public Task<int> GetUncheckedItemCountForGroupAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult(0);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearListItemsAsync(Guid shoppingListId, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteListAsync(Guid shoppingListId, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteGroupAsync(Guid groupId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> ToggleItemCheckedAsync(Guid itemId, bool isChecked, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> UpdateItemQuantityAsync(Guid itemId, decimal quantity, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> RemoveItemAsync(Guid itemId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default) => Task.FromResult<ShoppingListItem?>(null);
        public Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
