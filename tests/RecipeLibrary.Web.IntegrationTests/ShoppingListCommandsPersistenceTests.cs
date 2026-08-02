using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class ShoppingListCommandsPersistenceTests(SqlContainerFixture fixture)
{
    [Fact]
    public async Task RemoveItem_RemovesFromList()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        var queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        await bus.SendAsync<AddRecipesToShoppingListCommand, AddRecipesToShoppingListResult>(
            new AddRecipesToShoppingListCommand
            {
                ShoppingListId = fixture.Seed.ShoppingListId,
                RecipeIds = [fixture.Seed.RecipeId],
            });

        var group = await bus.SendAsync<EnsureShoppingListGroupCommand, EnsureShoppingListGroupResult>(
            new EnsureShoppingListGroupCommand
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
                OwnerUserId = TestDataSeeder.TestOwnerUserId,
                DefaultListNameFormat = "List {0}",
            });

        var itemId = group.Lists.First(l => l.Id == fixture.Seed.ShoppingListId).Items[0].Id;
        var remove = await bus.SendAsync<RemoveShoppingListItemCommand, RemoveShoppingListItemResult>(
            new RemoveShoppingListItemCommand { ItemId = itemId });

        Assert.True(remove.Removed);

        group = await bus.SendAsync<EnsureShoppingListGroupCommand, EnsureShoppingListGroupResult>(
            new EnsureShoppingListGroupCommand
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
                OwnerUserId = TestDataSeeder.TestOwnerUserId,
                DefaultListNameFormat = "List {0}",
            });

        var list = group.Lists.First(l => l.Id == fixture.Seed.ShoppingListId);
        Assert.DoesNotContain(list.Items, i => i.Id == itemId);
    }

    [Fact]
    public async Task ClearList_RemovesAllItems()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        var queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        await bus.SendAsync<AddRecipesToShoppingListCommand, AddRecipesToShoppingListResult>(
            new AddRecipesToShoppingListCommand
            {
                ShoppingListId = fixture.Seed.ShoppingListId,
                RecipeIds = [fixture.Seed.RecipeId],
            });

        var clear = await bus.SendAsync<ClearShoppingListCommand, ClearShoppingListResult>(
            new ClearShoppingListCommand { ShoppingListId = fixture.Seed.ShoppingListId });

        Assert.True(clear.Cleared);

        var group = await bus.SendAsync<EnsureShoppingListGroupCommand, EnsureShoppingListGroupResult>(
            new EnsureShoppingListGroupCommand
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
                OwnerUserId = TestDataSeeder.TestOwnerUserId,
                DefaultListNameFormat = "List {0}",
            });

        var list = group.Lists.First(l => l.Id == fixture.Seed.ShoppingListId);
        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task UpdateListName_PersistsNewName()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        var queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        var newName = $"Renamed {Guid.NewGuid():N}";
        var update = await bus.SendAsync<UpdateShoppingListNameCommand, UpdateShoppingListNameResult>(
            new UpdateShoppingListNameCommand
            {
                ShoppingListId = fixture.Seed.ShoppingListId,
                Name = newName,
            });

        Assert.True(update.Updated);

        var group = await bus.SendAsync<EnsureShoppingListGroupCommand, EnsureShoppingListGroupResult>(
            new EnsureShoppingListGroupCommand
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
                OwnerUserId = TestDataSeeder.TestOwnerUserId,
                DefaultListNameFormat = "List {0}",
            });

        Assert.Equal(newName, group.Lists.First(l => l.Id == fixture.Seed.ShoppingListId).Name);
    }

    [Fact]
    public async Task GetRecipeList_ReturnsSeededRecipe()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        var result = await queryBus.QueryAsync<GetRecipeListQuery, GetRecipeListResult>(
            new GetRecipeListQuery { Category = (int)RecipeLibrary.Domain.ValueObjects.RecipeCategory.Meat });

        Assert.Contains(result.Items, i => i.Id == fixture.Seed.RecipeId);
    }

    [Fact]
    public async Task DeleteRecipe_RemovesFromDatabase()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        var queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        var create = await bus.SendAsync<CreateRecipeCommand, CreateRecipeResult>(
            new CreateRecipeCommand
            {
                Title = $"Delete me {Guid.NewGuid():N}",
                Ingredients = [new CreateRecipeIngredientDto { Name = "Gehakt", Unit = "Gram", Quantity = 1 }],
                InstructionSteps = [new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Step" }],
            });

        var delete = await bus.SendAsync<DeleteRecipeCommand, DeleteRecipeResult>(
            new DeleteRecipeCommand { RecipeId = create.RecipeId });

        Assert.True(delete.Deleted);

        var loaded = await queryBus.QueryAsync<GetRecipeByIdQuery, GetRecipeByIdResult?>(
            new GetRecipeByIdQuery { RecipeId = create.RecipeId });

        Assert.Null(loaded);
    }

    [Fact]
    public async Task ClearList_ThrowsUnauthorized_WhenListOwnedByAnotherUser()
    {
        var foreign = await SeedForeignOwnedListAsync();

        using var scope = fixture.Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            bus.SendAsync<ClearShoppingListCommand, ClearShoppingListResult>(
                new ClearShoppingListCommand { ShoppingListId = foreign.ListId }));

        using var verifyScope = fixture.Factory.Services.CreateScope();
        var dbVerify = verifyScope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        var remaining = await dbVerify.ShoppingListItems
            .AsNoTracking()
            .CountAsync(i => i.ShoppingListId == foreign.ListId);
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task RemoveItem_ThrowsUnauthorized_WhenItemOwnedByAnotherUser()
    {
        var foreign = await SeedForeignOwnedListAsync();

        using var scope = fixture.Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            bus.SendAsync<RemoveShoppingListItemCommand, RemoveShoppingListItemResult>(
                new RemoveShoppingListItemCommand { ItemId = foreign.ItemId }));

        using var verifyScope = fixture.Factory.Services.CreateScope();
        var dbVerify = verifyScope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        Assert.True(await dbVerify.ShoppingListItems.AsNoTracking().AnyAsync(i => i.Id == foreign.ItemId));
    }

    [Fact]
    public async Task DeleteGroup_ThrowsUnauthorized_WhenGroupOwnedByAnotherUser()
    {
        var foreign = await SeedForeignOwnedListAsync();

        using var scope = fixture.Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            bus.SendAsync<DeleteShoppingListGroupCommand, DeleteShoppingListGroupResult>(
                new DeleteShoppingListGroupCommand { GroupId = foreign.GroupId }));

        using var verifyScope = fixture.Factory.Services.CreateScope();
        var dbVerify = verifyScope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        Assert.True(await dbVerify.ShoppingListGroups.AsNoTracking().AnyAsync(g => g.Id == foreign.GroupId));
        Assert.True(await dbVerify.ShoppingListItems.AsNoTracking().AnyAsync(i => i.Id == foreign.ItemId));
    }

    private async Task<(Guid GroupId, Guid ListId, Guid ItemId)> SeedForeignOwnedListAsync()
    {
        var foreignGroupId = Guid.NewGuid();
        var foreignListId = Guid.NewGuid();
        var foreignItemId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var seedScope = fixture.Factory.Services.CreateScope();
        var db = seedScope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        db.ShoppingListGroups.Add(new ShoppingListGroup
        {
            Id = foreignGroupId,
            OwnerUserId = $"foreign-owner-{Guid.NewGuid():N}",
            CreatedAt = now,
            UpdatedAt = now,
            Lists =
            [
                new ShoppingList
                {
                    Id = foreignListId,
                    GroupId = foreignGroupId,
                    Name = "Foreign list",
                    StoreOrder = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Items =
                    [
                        new ShoppingListItem
                        {
                            Id = foreignItemId,
                            ShoppingListId = foreignListId,
                            DisplayName = "Melk",
                            Quantity = new Quantity(1),
                            Unit = Unit.Piece,
                            SortOrder = 0,
                            IsChecked = false,
                        },
                    ],
                },
            ],
        });
        await db.SaveChangesAsync();

        return (foreignGroupId, foreignListId, foreignItemId);
    }
}
