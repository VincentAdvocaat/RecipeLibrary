using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class GetNextShoppingListNameQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsFirstName_WhenOwnerHasNoGroup()
    {
        var sut = new GetNextShoppingListNameQueryHandler(
            new RecordingShoppingListRepository(),
            new FixedCurrentUser("user-a"));

        var result = await sut.HandleAsync(new GetNextShoppingListNameQuery { NameFormat = "Shop {0}" });

        Assert.Equal("Shop 1", result.Name);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNextNumber_WhenOwnerGroupHasNames()
    {
        var groupId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            Group = new ShoppingListGroup { Id = groupId, OwnerUserId = "user-a" },
            ListNames = ["Shop 1", "Shop 2"],
        };
        var sut = new GetNextShoppingListNameQueryHandler(repo, new FixedCurrentUser("user-a"));

        var result = await sut.HandleAsync(new GetNextShoppingListNameQuery { NameFormat = "Shop {0}" });

        Assert.Equal("Shop 3", result.Name);
    }

    [Fact]
    public async Task HandleAsync_UsesScopedGroupNames_WhenScopeProvided()
    {
        var groupId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleGroupIds = { groupId },
            AccessibleByDefault = false,
            ListNames = ["Shop 1"],
        };
        var sut = new GetNextShoppingListNameQueryHandler(repo, new FixedCurrentUser("user-a"));

        var result = await sut.HandleAsync(new GetNextShoppingListNameQuery
        {
            NameFormat = "Shop {0}",
            ScopeGroupId = groupId,
        });

        Assert.Equal("Shop 2", result.Name);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenNameFormatMissing()
    {
        var sut = new GetNextShoppingListNameQueryHandler(
            new RecordingShoppingListRepository(),
            new FixedCurrentUser("user-a"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new GetNextShoppingListNameQuery { NameFormat = "  " }));
    }
}
