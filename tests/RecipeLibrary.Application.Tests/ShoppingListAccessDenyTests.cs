using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.UseCases.Pantry;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

/// <summary>
/// Simulates authenticated user B accessing resources owned by user A.
/// These paths are dormant in anonymous mode (OwnerUserId null) but must work when Entra is on.
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
        var sut = new ClearShoppingListCommandHandler(repo, new FixedCurrentUser(UserB));

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
        var sut = new ToggleShoppingListItemCommandHandler(repo, new FixedCurrentUser(UserB));

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
        var sut = new RemoveShoppingListItemCommandHandler(repo, new FixedCurrentUser(UserB));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new RemoveShoppingListItemCommand { ItemId = itemId }));

        Assert.Null(repo.LastRemovedItemId);
    }

    [Fact]
    public async Task DeleteGroup_Throws_WhenGroupNotAccessible()
    {
        var groupId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var sut = new DeleteShoppingListGroupCommandHandler(repo, new FixedCurrentUser(UserB));

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
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new UpsertPantryItemCommand
            {
                ShoppingListGroupId = groupId,
                DisplayName = "Zout",
            }));

        Assert.Null(pantry.UpsertedItem);
    }

    [Fact]
    public async Task AnonymousUser_SkipsAccessCheck_AndClearsList()
    {
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid() },
        };
        var sut = new ClearShoppingListCommandHandler(repo, new AnonymousCurrentUser());

        var result = await sut.HandleAsync(new ClearShoppingListCommand { ShoppingListId = listId });

        Assert.True(result.Cleared);
        Assert.Equal(listId, repo.LastClearedListId);
    }
}
