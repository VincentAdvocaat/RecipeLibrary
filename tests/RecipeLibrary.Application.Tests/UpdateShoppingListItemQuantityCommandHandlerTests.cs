using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class UpdateShoppingListItemQuantityCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_Throws_WhenQuantityInvalid()
    {
        var itemId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            Item = new ShoppingListItem
            {
                Id = itemId,
                Unit = Unit.Gram,
                Quantity = new Quantity(2),
            },
        };
        var sut = new UpdateShoppingListItemQuantityCommandHandler(repo, new FixedCurrentUser("user-a"), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new UpdateShoppingListItemQuantityCommand { ItemId = itemId, Quantity = 0 }));
    }

    [Fact]
    public async Task HandleAsync_UpdatesQuantity_WhenValid()
    {
        var itemId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            Item = new ShoppingListItem
            {
                Id = itemId,
                Unit = Unit.Gram,
                Quantity = new Quantity(2),
            },
            UpdateQuantityResult = true,
        };
        var sut = new UpdateShoppingListItemQuantityCommandHandler(repo, new FixedCurrentUser("user-a"), new NoOpUnitOfWork());

        var result = await sut.HandleAsync(new UpdateShoppingListItemQuantityCommand { ItemId = itemId, Quantity = 5 });

        Assert.True(result.Updated);
        Assert.Equal(5, result.Quantity);
        Assert.Equal(itemId, repo.LastQuantityItemId);
        Assert.Equal(5, repo.LastQuantity);
    }
}
