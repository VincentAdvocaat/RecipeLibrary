using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class AddManualShoppingListItemCommandHandlerTests
{
    private readonly ShoppingListIngredientMerger _merger = new(new IngredientTextNormalizer());

    [Fact]
    public async Task HandleAsync_Throws_WhenNameEmpty()
    {
        var sut = new AddManualShoppingListItemCommandHandler(new FakeShoppingListRepository(), _merger);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new AddManualShoppingListItemCommand
            {
                ShoppingListId = Guid.NewGuid(),
                DisplayName = "  ",
                Quantity = 1,
                Unit = nameof(Unit.Gram),
            }));
    }

    [Fact]
    public async Task HandleAsync_AddsManualItem_ToList()
    {
        var listId = Guid.NewGuid();
        var repo = new FakeShoppingListRepository
        {
            List = new ShoppingList
            {
                Id = listId,
                Items = [],
            },
        };
        var sut = new AddManualShoppingListItemCommandHandler(repo, _merger);

        var result = await sut.HandleAsync(new AddManualShoppingListItemCommand
        {
            ShoppingListId = listId,
            DisplayName = "Melk",
            Quantity = 2,
            Unit = nameof(Unit.Piece),
        });

        Assert.True(result.Added);
        Assert.NotNull(result.ItemId);
        Assert.Equal(listId, repo.LastReplacedListId);
        Assert.Single(repo.LastReplacedItems!);
        Assert.Equal("Melk", repo.LastReplacedItems![0].DisplayName);
        Assert.Equal(2, repo.LastReplacedItems[0].Quantity.Value);
        Assert.Empty(repo.LastReplacedItems[0].Sources);
    }

    [Fact]
    public async Task HandleAsync_MergesQuantity_WhenMatchingManualItemExists()
    {
        var listId = Guid.NewGuid();
        var existing = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = listId,
            DisplayName = "Melk",
            Quantity = new Quantity(1),
            Unit = Unit.Piece,
            Sources = [],
        };
        var repo = new FakeShoppingListRepository
        {
            List = new ShoppingList
            {
                Id = listId,
                Items = [existing],
            },
        };
        var sut = new AddManualShoppingListItemCommandHandler(repo, _merger);

        await sut.HandleAsync(new AddManualShoppingListItemCommand
        {
            ShoppingListId = listId,
            DisplayName = "Melk",
            Quantity = 2,
            Unit = nameof(Unit.Piece),
        });

        Assert.Single(repo.LastReplacedItems!);
        Assert.Equal(existing.Id, repo.LastReplacedItems![0].Id);
        Assert.Equal(3, repo.LastReplacedItems[0].Quantity.Value);
    }

    private sealed class FakeShoppingListRepository : IShoppingListRepository
    {
        public ShoppingList? List { get; init; }
        public Guid? LastReplacedListId { get; private set; }
        public IReadOnlyList<ShoppingListItem>? LastReplacedItems { get; private set; }

        public Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default) =>
            Task.FromResult(List?.Id == listId ? List : null);

        public Task ReplaceListItemsAsync(Guid shoppingListId, IReadOnlyList<ShoppingListItem> items, CancellationToken ct = default)
        {
            LastReplacedListId = shoppingListId;
            LastReplacedItems = items;
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
        public Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default) => Task.FromResult<ShoppingListItem?>(null);
        public Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> UpdateItemQuantityAsync(Guid itemId, decimal quantity, CancellationToken ct = default) => Task.FromResult(false);
    }
}
