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
        var sut = new AddManualShoppingListItemCommandHandler(
            new RecordingShoppingListRepository(),
            new FixedCurrentUser("user-a"),
            _merger,
            new NoOpUnitOfWork());

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
        var repo = new RecordingShoppingListRepository
        {
            List = new ShoppingList
            {
                Id = listId,
                Items = [],
            },
        };
        var sut = new AddManualShoppingListItemCommandHandler(
            repo,
            new FixedCurrentUser("user-a"),
            _merger,
            new NoOpUnitOfWork());

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
        Assert.Equal(2, repo.LastReplacedItems[0].Quantity!.Value.Value);
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
        var repo = new RecordingShoppingListRepository
        {
            List = new ShoppingList
            {
                Id = listId,
                Items = [existing],
            },
        };
        var sut = new AddManualShoppingListItemCommandHandler(
            repo,
            new FixedCurrentUser("user-a"),
            _merger,
            new NoOpUnitOfWork());

        await sut.HandleAsync(new AddManualShoppingListItemCommand
        {
            ShoppingListId = listId,
            DisplayName = "Melk",
            Quantity = 2,
            Unit = nameof(Unit.Piece),
        });

        Assert.Single(repo.LastReplacedItems!);
        Assert.Equal(existing.Id, repo.LastReplacedItems![0].Id);
        Assert.Equal(3, repo.LastReplacedItems[0].Quantity!.Value.Value);
    }
}
