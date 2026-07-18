using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.UseCases.Pantry;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class PantryCommandHandlerTests
{
    [Fact]
    public async Task GetPantryItems_ReturnsMappedItems_ForGroupOwnerKey()
    {
        var groupId = Guid.NewGuid();
        var ownerKey = $"group:{groupId:D}";
        var pantry = new RecordingPantryRepository
        {
            Items =
            [
                new PantryItem
                {
                    Id = Guid.NewGuid(),
                    OwnerUserId = ownerKey,
                    DisplayName = "Zout",
                },
                new PantryItem
                {
                    Id = Guid.NewGuid(),
                    OwnerUserId = "other-owner",
                    DisplayName = "Ignored",
                },
            ],
        };
        var sut = new GetPantryItemsQueryHandler(
            pantry,
            new RecordingShoppingListRepository(),
            new AnonymousShoppingListUserContext());

        var result = await sut.HandleAsync(new GetPantryItemsQuery { ShoppingListGroupId = groupId });

        Assert.Equal(ownerKey, pantry.LastQueriedOwnerKey);
        Assert.Single(result.Items);
        Assert.Equal("Zout", result.Items[0].DisplayName);
    }

    [Fact]
    public async Task GetPantryItems_UsesAuthenticatedOwnerUserId_AsOwnerKey()
    {
        var groupId = Guid.NewGuid();
        var pantry = new RecordingPantryRepository
        {
            Items =
            [
                new PantryItem
                {
                    Id = Guid.NewGuid(),
                    OwnerUserId = "user-42",
                    DisplayName = "Olie",
                },
            ],
        };
        var shopping = new RecordingShoppingListRepository();
        shopping.AccessibleGroupIds.Add(groupId);
        var sut = new GetPantryItemsQueryHandler(
            pantry,
            shopping,
            new FixedShoppingListUserContext("user-42"));

        // AccessibleByDefault is true, so access passes.
        var result = await sut.HandleAsync(new GetPantryItemsQuery { ShoppingListGroupId = groupId });

        Assert.Equal("user-42", pantry.LastQueriedOwnerKey);
        Assert.Single(result.Items);
        Assert.Equal("Olie", result.Items[0].DisplayName);
    }

    [Fact]
    public async Task UpsertPantryItem_AddsNewStaple()
    {
        var groupId = Guid.NewGuid();
        var pantry = new RecordingPantryRepository();
        var sut = new UpsertPantryItemCommandHandler(
            pantry,
            new RecordingShoppingListRepository(),
            new AnonymousShoppingListUserContext(),
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        var result = await sut.HandleAsync(new UpsertPantryItemCommand
        {
            ShoppingListGroupId = groupId,
            DisplayName = "  Peper  ",
        });

        Assert.True(result.Upserted);
        Assert.NotEqual(Guid.Empty, result.ItemId);
        Assert.NotNull(pantry.UpsertedItem);
        Assert.Equal("Peper", pantry.UpsertedItem!.DisplayName);
        Assert.Equal($"group:{groupId:D}", pantry.UpsertedItem.OwnerUserId);
    }

    [Fact]
    public async Task UpsertPantryItem_Throws_WhenDisplayNameEmpty()
    {
        var sut = new UpsertPantryItemCommandHandler(
            new RecordingPantryRepository(),
            new RecordingShoppingListRepository(),
            new AnonymousShoppingListUserContext(),
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new UpsertPantryItemCommand
            {
                ShoppingListGroupId = Guid.NewGuid(),
                DisplayName = "   ",
            }));
    }

    [Fact]
    public async Task UpsertPantryItem_IsIdempotent_WhenAlreadyPresent()
    {
        var groupId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        var ownerKey = $"group:{groupId:D}";
        var pantry = new RecordingPantryRepository
        {
            Items =
            [
                new PantryItem
                {
                    Id = existingId,
                    OwnerUserId = ownerKey,
                    DisplayName = "Zout",
                },
            ],
        };
        var sut = new UpsertPantryItemCommandHandler(
            pantry,
            new RecordingShoppingListRepository(),
            new AnonymousShoppingListUserContext(),
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        var result = await sut.HandleAsync(new UpsertPantryItemCommand
        {
            ShoppingListGroupId = groupId,
            DisplayName = "zout",
        });

        Assert.Equal(existingId, result.ItemId);
    }

    [Fact]
    public async Task RemovePantryItem_RemovesForOwnerKey()
    {
        var groupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var ownerKey = $"group:{groupId:D}";
        var pantry = new RecordingPantryRepository
        {
            Items =
            [
                new PantryItem
                {
                    Id = itemId,
                    OwnerUserId = ownerKey,
                    DisplayName = "Zout",
                },
            ],
        };
        var sut = new RemovePantryItemCommandHandler(
            pantry,
            new RecordingShoppingListRepository(),
            new AnonymousShoppingListUserContext());

        var result = await sut.HandleAsync(new RemovePantryItemCommand
        {
            ShoppingListGroupId = groupId,
            ItemId = itemId,
        });

        Assert.True(result.Removed);
        Assert.Equal(itemId, pantry.LastRemovedItemId);
        Assert.Equal(ownerKey, pantry.LastRemovedOwnerKey);
        Assert.Empty(pantry.Items);
    }

    [Fact]
    public async Task ApplyPantry_RemovesMatchingShoppingListLines()
    {
        var listId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var ownerKey = $"group:{groupId:D}";
        var keepId = Guid.NewGuid();
        var removeId = Guid.NewGuid();
        var shopping = new RecordingShoppingListRepository
        {
            List = new ShoppingList
            {
                Id = listId,
                GroupId = groupId,
                Items =
                [
                    new ShoppingListItem
                    {
                        Id = removeId,
                        ShoppingListId = listId,
                        DisplayName = "Zout",
                        Quantity = new Quantity(1),
                        Unit = Unit.Teaspoon,
                    },
                    new ShoppingListItem
                    {
                        Id = keepId,
                        ShoppingListId = listId,
                        DisplayName = "Gehakt",
                        Quantity = new Quantity(500),
                        Unit = Unit.Gram,
                    },
                ],
            },
        };
        var pantry = new RecordingPantryRepository
        {
            Items =
            [
                new PantryItem
                {
                    Id = Guid.NewGuid(),
                    OwnerUserId = ownerKey,
                    DisplayName = "Zout",
                },
            ],
        };
        var merger = new PantryIngredientMerger(new IngredientTextNormalizer());
        var sut = new ApplyPantryToShoppingListCommandHandler(
            shopping,
            pantry,
            new AnonymousShoppingListUserContext(),
            new PantryExclusionFilter(merger));

        var result = await sut.HandleAsync(new ApplyPantryToShoppingListCommand { ShoppingListId = listId });

        Assert.Equal(1, result.ItemsRemoved);
        Assert.Equal(listId, shopping.LastReplacedListId);
        Assert.NotNull(shopping.LastReplacedItems);
        Assert.Single(shopping.LastReplacedItems!);
        Assert.Equal(keepId, shopping.LastReplacedItems![0].Id);
    }

    [Fact]
    public async Task ApplyPantry_ReturnsZero_WhenPantryEmpty()
    {
        var listId = Guid.NewGuid();
        var shopping = new RecordingShoppingListRepository
        {
            List = new ShoppingList
            {
                Id = listId,
                GroupId = Guid.NewGuid(),
                Items =
                [
                    new ShoppingListItem
                    {
                        Id = Guid.NewGuid(),
                        ShoppingListId = listId,
                        DisplayName = "Melk",
                        Quantity = new Quantity(1),
                        Unit = Unit.Piece,
                    },
                ],
            },
        };
        var merger = new PantryIngredientMerger(new IngredientTextNormalizer());
        var sut = new ApplyPantryToShoppingListCommandHandler(
            shopping,
            new RecordingPantryRepository(),
            new AnonymousShoppingListUserContext(),
            new PantryExclusionFilter(merger));

        var result = await sut.HandleAsync(new ApplyPantryToShoppingListCommand { ShoppingListId = listId });

        Assert.Equal(0, result.ItemsRemoved);
        Assert.Null(shopping.LastReplacedListId);
    }
}
