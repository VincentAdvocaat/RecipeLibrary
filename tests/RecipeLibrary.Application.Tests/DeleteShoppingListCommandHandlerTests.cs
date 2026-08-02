using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class DeleteShoppingListCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsFalse_WhenListMissing()
    {
        var repo = new RecordingShoppingListRepository();
        var sut = new DeleteShoppingListCommandHandler(repo, new FixedCurrentUser("user-a"), new NoOpUnitOfWork());

        var result = await sut.HandleAsync(new DeleteShoppingListCommand { ShoppingListId = Guid.NewGuid() });

        Assert.False(result.Deleted);
        Assert.Null(result.RemainingGroupId);
    }

    [Fact]
    public async Task HandleAsync_DeletesListAndReturnsGroupId()
    {
        var listId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var list = new ShoppingList { Id = listId, GroupId = groupId, Name = "Main" };
        var group = new ShoppingListGroup { Id = groupId };
        var repo = new RecordingShoppingListRepository { List = list, Group = group };
        var sut = new DeleteShoppingListCommandHandler(repo, new FixedCurrentUser("user-a"), new NoOpUnitOfWork());

        var result = await sut.HandleAsync(new DeleteShoppingListCommand { ShoppingListId = listId });

        Assert.True(result.Deleted);
        Assert.Equal(groupId, result.RemainingGroupId);
        Assert.True(repo.DeleteListCalled);
    }
}
