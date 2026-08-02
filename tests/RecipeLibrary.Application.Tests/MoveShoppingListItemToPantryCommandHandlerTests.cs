using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class MoveShoppingListItemToPantryCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpsertsPantryAndRemovesShoppingListItem()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var shoppingRepo = new RecordingShoppingListRepository
        {
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Zout",
                Quantity = new Quantity(1),
                Unit = Unit.Teaspoon,
            },
            List = new ShoppingList { Id = listId, GroupId = groupId },
            RemoveResult = true,
        };
        var pantryRepo = new RecordingPantryRepository();
        var unitOfWork = new NoOpUnitOfWork();
        var sut = new MoveShoppingListItemToPantryCommandHandler(
            shoppingRepo,
            pantryRepo,
            new FixedCurrentUser("user-a"),
            unitOfWork,
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        var result = await sut.HandleAsync(new MoveShoppingListItemToPantryCommand { ItemId = itemId });

        Assert.True(result.Moved);
        Assert.True(unitOfWork.Executed);
        Assert.NotNull(pantryRepo.UpsertedItem);
        Assert.Equal("Zout", pantryRepo.UpsertedItem!.DisplayName);
        Assert.Equal("user-a", pantryRepo.UpsertedItem.OwnerUserId);
        Assert.Equal(itemId, shoppingRepo.LastRemovedItemId);
    }

    [Fact]
    public async Task HandleAsync_IsIdempotent_WhenAlreadyInPantry()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var existingPantryId = Guid.NewGuid();
        const string ownerKey = "user-a";
        var shoppingRepo = new RecordingShoppingListRepository
        {
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Zout",
                Quantity = new Quantity(1),
                Unit = Unit.Teaspoon,
            },
            List = new ShoppingList { Id = listId, GroupId = groupId },
            RemoveResult = true,
        };
        var pantryRepo = new RecordingPantryRepository
        {
            Items =
            [
                new PantryItem
                {
                    Id = existingPantryId,
                    OwnerUserId = ownerKey,
                    DisplayName = "Zout",
                },
            ],
        };
        var sut = new MoveShoppingListItemToPantryCommandHandler(
            shoppingRepo,
            pantryRepo,
            new FixedCurrentUser(ownerKey),
            new NoOpUnitOfWork(),
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        var result = await sut.HandleAsync(new MoveShoppingListItemToPantryCommand { ItemId = itemId });

        Assert.True(result.Moved);
        Assert.Equal(existingPantryId, result.PantryItemId);
        Assert.Equal(itemId, shoppingRepo.LastRemovedItemId);
    }

    [Fact]
    public async Task HandleAsync_UsesAuthenticatedOwnerUserId_ForPantryKey()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var shoppingRepo = new RecordingShoppingListRepository
        {
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Olie",
                Quantity = new Quantity(1),
                Unit = Unit.Tablespoon,
            },
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid() },
            RemoveResult = true,
        };
        var pantryRepo = new RecordingPantryRepository();
        var sut = new MoveShoppingListItemToPantryCommandHandler(
            shoppingRepo,
            pantryRepo,
            new FixedCurrentUser("user-42"),
            new NoOpUnitOfWork(),
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        await sut.HandleAsync(new MoveShoppingListItemToPantryCommand { ItemId = itemId });

        Assert.Equal("user-42", pantryRepo.UpsertedItem!.OwnerUserId);
    }
}
