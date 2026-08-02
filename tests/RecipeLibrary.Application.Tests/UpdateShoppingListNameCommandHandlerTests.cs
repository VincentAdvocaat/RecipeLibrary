using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class UpdateShoppingListNameCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_Throws_WhenNameEmpty()
    {
        var sut = new UpdateShoppingListNameCommandHandler(
            new RecordingShoppingListRepository(),
            new FixedCurrentUser("user-a"),
            new NoOpUnitOfWork());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new UpdateShoppingListNameCommand { ShoppingListId = Guid.NewGuid(), Name = "  " }));
    }

    [Fact]
    public async Task HandleAsync_ReturnsUpdated_WhenRepositorySucceeds()
    {
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository { UpdateNameResult = true };
        var sut = new UpdateShoppingListNameCommandHandler(repo, new FixedCurrentUser("user-a"), new NoOpUnitOfWork());

        var result = await sut.HandleAsync(new UpdateShoppingListNameCommand { ShoppingListId = listId, Name = "Store 2" });

        Assert.True(result.Updated);
        Assert.Equal(listId, repo.LastUpdatedListId);
        Assert.Equal("Store 2", repo.LastUpdatedName);
    }
}
