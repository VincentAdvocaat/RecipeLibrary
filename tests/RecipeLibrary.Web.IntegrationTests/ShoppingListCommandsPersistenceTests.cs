using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Contracts;
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

        var group = await queryBus.QueryAsync<GetOrCreateShoppingListGroupQuery, GetOrCreateShoppingListGroupResult>(
            new GetOrCreateShoppingListGroupQuery
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
                DefaultListNameFormat = "List {0}",
            });

        var itemId = group.Lists.First(l => l.Id == fixture.Seed.ShoppingListId).Items[0].Id;
        var remove = await bus.SendAsync<RemoveShoppingListItemCommand, RemoveShoppingListItemResult>(
            new RemoveShoppingListItemCommand { ItemId = itemId });

        Assert.True(remove.Removed);

        group = await queryBus.QueryAsync<GetOrCreateShoppingListGroupQuery, GetOrCreateShoppingListGroupResult>(
            new GetOrCreateShoppingListGroupQuery
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
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

        var group = await queryBus.QueryAsync<GetOrCreateShoppingListGroupQuery, GetOrCreateShoppingListGroupResult>(
            new GetOrCreateShoppingListGroupQuery
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
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

        var group = await queryBus.QueryAsync<GetOrCreateShoppingListGroupQuery, GetOrCreateShoppingListGroupResult>(
            new GetOrCreateShoppingListGroupQuery
            {
                GroupId = fixture.Seed.ShoppingListGroupId,
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
}
