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

        var repo = new RecordingShoppingListRepository
        {
            List = primary,
            SecondaryListId = secondaryId,
        };
        var sut = new SplitShoppingListCommandHandler(
            repo,
            new FixedCurrentUser("user-a"),
            new ShoppingListIngredientMerger(new IngredientTextNormalizer()),
            new NoOpUnitOfWork());

        var result = await sut.HandleAsync(new SplitShoppingListCommand
        {
            GroupId = groupId,
            NewListName = "Store 2",
            ItemIds = [itemId],
        });

        Assert.Equal(secondaryId, result.NewListId);
        Assert.Equal(1, result.ItemsMoved);
        Assert.True(repo.ReplacedItemsByListId.ContainsKey(primaryId));
        Assert.Single(repo.ReplacedItemsByListId[primaryId]);
        Assert.True(repo.ReplacedItemsByListId.ContainsKey(secondaryId));
        Assert.Single(repo.ReplacedItemsByListId[secondaryId]);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenNameEmpty()
    {
        var sut = new SplitShoppingListCommandHandler(
            new RecordingShoppingListRepository(),
            new FixedCurrentUser("user-a"),
            new ShoppingListIngredientMerger(new IngredientTextNormalizer()),
            new NoOpUnitOfWork());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new SplitShoppingListCommand { GroupId = Guid.NewGuid(), NewListName = "", ItemIds = [Guid.NewGuid()] }));
    }
}
