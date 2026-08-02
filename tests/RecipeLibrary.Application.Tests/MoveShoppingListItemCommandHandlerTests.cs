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
        var repo = new RecordingShoppingListRepository { Item = item };
        repo.ListsById[sourceListId] = sourceList;
        repo.ListsById[targetListId] = targetList;
        var sut = new MoveShoppingListItemCommandHandler(
            repo,
            new FixedCurrentUser("user-a"),
            new ShoppingListIngredientMerger(new IngredientTextNormalizer()),
            new NoOpUnitOfWork());

        var result = await sut.HandleAsync(new MoveShoppingListItemCommand
        {
            ItemId = itemId,
            TargetShoppingListId = targetListId,
        });

        Assert.True(result.Moved);
        Assert.True(repo.ReplacedItemsByListId.ContainsKey(sourceListId));
        Assert.Empty(repo.ReplacedItemsByListId[sourceListId]);
        Assert.True(repo.ReplacedItemsByListId.ContainsKey(targetListId));
        Assert.Single(repo.ReplacedItemsByListId[targetListId]);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTrue_WhenItemAlreadyOnTargetList()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var item = new ShoppingListItem { Id = itemId, ShoppingListId = listId, DisplayName = "Gehakt" };
        var list = new ShoppingList { Id = listId, GroupId = Guid.NewGuid(), Items = [item] };
        var repo = new RecordingShoppingListRepository { Item = item };
        repo.ListsById[listId] = list;
        var sut = new MoveShoppingListItemCommandHandler(
            repo,
            new FixedCurrentUser("user-a"),
            new ShoppingListIngredientMerger(new IngredientTextNormalizer()),
            new NoOpUnitOfWork());

        var result = await sut.HandleAsync(new MoveShoppingListItemCommand { ItemId = itemId, TargetShoppingListId = listId });

        Assert.True(result.Moved);
        Assert.Empty(repo.ReplacedItemsByListId);
    }
}
