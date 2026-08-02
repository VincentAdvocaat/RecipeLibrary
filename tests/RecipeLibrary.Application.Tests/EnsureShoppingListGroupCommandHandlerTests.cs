using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class EnsureShoppingListGroupCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsExistingGroup_WhenOwnerHasGroup()
    {
        const string ownerUserId = "user-a";
        var groupId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var group = new ShoppingListGroup
        {
            Id = groupId,
            OwnerUserId = ownerUserId,
            Lists =
            [
                new ShoppingList { Id = listId, GroupId = groupId, Name = "List 1", StoreOrder = 1 },
            ],
        };

        var repo = new RecordingShoppingListRepository { Group = group };
        var sut = new EnsureShoppingListGroupCommandHandler(repo, new NoOpUnitOfWork());

        var result = await sut.HandleAsync(new EnsureShoppingListGroupCommand
        {
            OwnerUserId = ownerUserId,
            DefaultListNameFormat = "List {0}",
        });

        Assert.Equal(groupId, result.GroupId);
        Assert.Single(result.Lists);
        Assert.Equal("List 1", result.Lists[0].Name);
        Assert.False(repo.CreateGroupCalled);
    }

    [Fact]
    public async Task HandleAsync_CreatesGroup_WhenNoGroupExists()
    {
        const string ownerUserId = "user-a";
        var createdGroupId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            CreatedGroup = new ShoppingListGroup
            {
                Id = createdGroupId,
                OwnerUserId = ownerUserId,
                Lists = [new ShoppingList { Id = listId, GroupId = createdGroupId, Name = "List 1", StoreOrder = 1 }],
            },
        };

        var sut = new EnsureShoppingListGroupCommandHandler(repo, new NoOpUnitOfWork());

        var result = await sut.HandleAsync(new EnsureShoppingListGroupCommand
        {
            OwnerUserId = ownerUserId,
            DefaultListNameFormat = "List {0}",
        });

        Assert.Equal(createdGroupId, result.GroupId);
        Assert.True(repo.CreateGroupCalled);
        Assert.Equal(ownerUserId, repo.LastCreateOwnerUserId);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenOwnerUserIdMissing()
    {
        var sut = new EnsureShoppingListGroupCommandHandler(new RecordingShoppingListRepository(), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new EnsureShoppingListGroupCommand
            {
                OwnerUserId = null,
                DefaultListNameFormat = "List {0}",
            }));
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenNameFormatMissing()
    {
        var sut = new EnsureShoppingListGroupCommandHandler(new RecordingShoppingListRepository(), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new EnsureShoppingListGroupCommand
            {
                OwnerUserId = "user-a",
                DefaultListNameFormat = "  ",
            }));
    }
}
