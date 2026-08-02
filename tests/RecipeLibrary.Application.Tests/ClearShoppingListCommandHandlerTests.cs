using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class ClearShoppingListCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsFalse_WhenListDoesNotExist()
    {
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository();
        var sut = new ClearShoppingListCommandHandler(repo, new FixedCurrentUser("user-a"), new NoOpUnitOfWork());

        var result = await sut.HandleAsync(new ClearShoppingListCommand { ShoppingListId = listId });

        Assert.False(result.Cleared);
        Assert.Null(repo.LastClearedListId);
    }

    [Fact]
    public async Task HandleAsync_ClearsItems_WhenListExists()
    {
        var listId = Guid.NewGuid();
        var list = new ShoppingList { Id = listId, GroupId = Guid.NewGuid(), Name = "Main" };
        var repo = new RecordingShoppingListRepository { List = list };
        var sut = new ClearShoppingListCommandHandler(repo, new FixedCurrentUser("user-a"), new NoOpUnitOfWork());

        var result = await sut.HandleAsync(new ClearShoppingListCommand { ShoppingListId = listId });

        Assert.True(result.Cleared);
        Assert.Equal(listId, repo.LastClearedListId);
    }
}
