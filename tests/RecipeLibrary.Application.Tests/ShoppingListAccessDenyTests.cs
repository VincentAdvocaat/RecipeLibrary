using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Application.UseCases.Pantry;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

/// <summary>
/// Fail-closed access: unauthenticated callers and non-owners are denied.
/// </summary>
public sealed class ShoppingListAccessDenyTests
{
    private const string UserB = "user-b";

    [Fact]
    public async Task ClearShoppingList_Throws_WhenListNotAccessible()
    {
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid() },
        };
        var sut = new ClearShoppingListCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new ClearShoppingListCommand { ShoppingListId = listId }));

        Assert.Null(repo.LastClearedListId);
    }

    [Fact]
    public async Task ToggleItem_Throws_WhenListNotAccessible()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Melk",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
            },
        };
        var sut = new ToggleShoppingListItemCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new ToggleShoppingListItemCommand { ItemId = itemId, IsChecked = true }));

        Assert.Null(repo.LastToggledItemId);
    }

    [Fact]
    public async Task RemoveItem_Throws_WhenListNotAccessible()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Melk",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
            },
        };
        var sut = new RemoveShoppingListItemCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new RemoveShoppingListItemCommand { ItemId = itemId }));

        Assert.Null(repo.LastRemovedItemId);
    }

    [Fact]
    public async Task DeleteGroup_Throws_WhenGroupNotAccessible()
    {
        var groupId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var sut = new DeleteShoppingListGroupCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new DeleteShoppingListGroupCommand { GroupId = groupId }));

        Assert.Null(repo.LastDeletedGroupId);
    }

    [Fact]
    public async Task GetSummary_Throws_WhenGroupNotAccessible()
    {
        var groupId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            UncheckedItemCount = 99,
        };
        var sut = new GetShoppingListSummaryQueryHandler(repo, new FixedCurrentUser(UserB));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new GetShoppingListSummaryQuery { GroupId = groupId }));
    }

    [Fact]
    public async Task GetPantryItems_Throws_WhenGroupNotAccessible()
    {
        var groupId = Guid.NewGuid();
        var shopping = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var pantry = new RecordingPantryRepository();
        var sut = new GetPantryItemsQueryHandler(pantry, shopping, new FixedCurrentUser(UserB));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new GetPantryItemsQuery { ShoppingListGroupId = groupId }));

        Assert.Null(pantry.LastQueriedOwnerKey);
    }

    [Fact]
    public async Task UpsertPantryItem_Throws_WhenGroupNotAccessible()
    {
        var groupId = Guid.NewGuid();
        var shopping = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var pantry = new RecordingPantryRepository();
        var sut = new UpsertPantryItemCommandHandler(
            pantry,
            shopping,
            new FixedCurrentUser(UserB),
            new PantryIngredientMerger(new IngredientTextNormalizer()), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new UpsertPantryItemCommand
            {
                ShoppingListGroupId = groupId,
                DisplayName = "Zout",
            }));

        Assert.Null(pantry.UpsertedItem);
    }

    [Fact]
    public async Task AnonymousUser_Throws_WhenClearingList()
    {
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid() },
        };
        var sut = new ClearShoppingListCommandHandler(repo, new AnonymousCurrentUser(), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new ClearShoppingListCommand { ShoppingListId = listId }));

        Assert.Null(repo.LastClearedListId);
    }

    [Fact]
    public async Task AnonymousUser_Throws_WhenGettingSummary()
    {
        var groupId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            UncheckedItemCount = 3,
        };
        var sut = new GetShoppingListSummaryQueryHandler(repo, new AnonymousCurrentUser());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new GetShoppingListSummaryQuery { GroupId = groupId }));
    }

    [Fact]
    public async Task AnonymousUser_Throws_WhenTogglingItem()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Melk",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
            },
        };
        var sut = new ToggleShoppingListItemCommandHandler(repo, new AnonymousCurrentUser(), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new ToggleShoppingListItemCommand { ItemId = itemId, IsChecked = true }));

        Assert.Null(repo.LastToggledItemId);
    }

    [Fact]
    public async Task AddManualItem_Throws_WhenListNotAccessible()
    {
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid(), Items = [] },
        };
        var sut = new AddManualShoppingListItemCommandHandler(
            repo,
            new FixedCurrentUser(UserB),
            new ShoppingListIngredientMerger(new IngredientTextNormalizer()), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new AddManualShoppingListItemCommand
            {
                ShoppingListId = listId,
                DisplayName = "Melk",
                Quantity = 1,
                Unit = nameof(Unit.Piece),
            }));

        Assert.Null(repo.LastReplacedListId);
    }

    [Fact]
    public async Task UpdateItemQuantity_Throws_WhenListNotAccessible()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Melk",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
            },
        };
        var sut = new UpdateShoppingListItemQuantityCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new UpdateShoppingListItemQuantityCommand { ItemId = itemId, Quantity = 3 }));

        Assert.Null(repo.LastQuantityItemId);
    }

    [Fact]
    public async Task AnonymousUser_Throws_BeforeItemLookup_WhenItemMissing()
    {
        var itemId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var sut = new ToggleShoppingListItemCommandHandler(repo, new AnonymousCurrentUser(), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new ToggleShoppingListItemCommand { ItemId = itemId, IsChecked = true }));

        Assert.Null(repo.LastToggledItemId);
    }

    [Fact]
    public async Task ToggleItem_Throws_WhenItemMissing_ForAuthenticatedUser()
    {
        var repo = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var sut = new ToggleShoppingListItemCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.HandleAsync(new ToggleShoppingListItemCommand { ItemId = Guid.NewGuid(), IsChecked = true }));
    }
}
