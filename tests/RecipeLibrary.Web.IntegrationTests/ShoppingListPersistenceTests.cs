using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class ShoppingListPersistenceTests(SqlContainerFixture fixture)
{
    [Fact]
    public async Task AddRecipes_MergesItems_AndToggleWorks()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        var queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        var add = await bus.SendAsync<AddRecipesToShoppingListCommand, AddRecipesToShoppingListResult>(
            new AddRecipesToShoppingListCommand
            {
                ShoppingListId = fixture.Seed.ShoppingListId,
                RecipeIds = [fixture.Seed.RecipeId],
            });

        Assert.Equal(1, add.RecipesAdded);
        Assert.True(add.IngredientsAdded > 0);

        var group = await queryBus.QueryAsync<GetOrCreateShoppingListGroupQuery, GetOrCreateShoppingListGroupResult>(
            new GetOrCreateShoppingListGroupQuery
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
                DefaultListNameFormat = "List {0}",
            });

        var list = group.Lists.First(l => l.Id == fixture.Seed.ShoppingListId);
        Assert.NotEmpty(list.Items);
        var itemId = list.Items[0].Id;

        await bus.SendAsync<ToggleShoppingListItemCommand, ToggleShoppingListItemResult>(
            new ToggleShoppingListItemCommand { ItemId = itemId, IsChecked = true });

        group = await queryBus.QueryAsync<GetOrCreateShoppingListGroupQuery, GetOrCreateShoppingListGroupResult>(
            new GetOrCreateShoppingListGroupQuery
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
                DefaultListNameFormat = "List {0}",
            });

        list = group.Lists.First(l => l.Id == fixture.Seed.ShoppingListId);
        Assert.True(list.Items.First(i => i.Id == itemId).IsChecked);
    }

    [Fact]
    public async Task SplitShoppingList_CreatesSecondList()
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

        var group = await queryBus.QueryAsync<GetOrCreateShoppingListGroupQuery, GetOrCreateShoppingListGroupResult>(
            new GetOrCreateShoppingListGroupQuery
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
                DefaultListNameFormat = "List {0}",
            });

        var primaryList = group.Lists.First(l => l.Id == fixture.Seed.ShoppingListId);
        var itemId = primaryList.Items[0].Id;

        var split = await bus.SendAsync<SplitShoppingListCommand, SplitShoppingListResult>(
            new SplitShoppingListCommand
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
                NewListName = "Store 2",
                ItemIds = [itemId],
            });

        Assert.NotEqual(Guid.Empty, split.NewListId);
        Assert.Equal(1, split.ItemsMoved);

        group = await queryBus.QueryAsync<GetOrCreateShoppingListGroupQuery, GetOrCreateShoppingListGroupResult>(
            new GetOrCreateShoppingListGroupQuery
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
                DefaultListNameFormat = "List {0}",
            });

        Assert.Equal(2, group.Lists.Count);
    }
}
