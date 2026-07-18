using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.ValueObjects;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class RecipePersistenceTests(SqlContainerFixture fixture)
{
    [Fact]
    public async Task CreateRecipe_WithPreparation_PersistsAndLoads()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        var queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        var create = await bus.SendAsync<CreateRecipeCommand, CreateRecipeResult>(
            new CreateRecipeCommand
            {
                Title = "Integration Test Recipe",
                Category = 2,
                PreparationTimeMinutes = 10,
                CookingTimeMinutes = 20,
                Ingredients =
                [
                    new CreateRecipeIngredientDto
                    {
                        Name = "Gehakt",
                        Preparation = "ruim",
                        Quantity = 250,
                        Unit = Unit.Gram.ToString(),
                    },
                ],
                InstructionSteps =
                [
                    new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Cook." },
                ],
            });

        var loaded = await queryBus.QueryAsync<GetRecipeByIdQuery, GetRecipeByIdResult?>(
            new GetRecipeByIdQuery { RecipeId = create.RecipeId });

        Assert.NotNull(loaded);
        Assert.Equal("Integration Test Recipe", loaded.Title);
        Assert.Single(loaded.Ingredients);
        Assert.Equal("ruim", loaded.Ingredients[0].Preparation);
    }

    [Fact]
    public async Task GetRecipeList_FiltersBySearchAndCategory()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        var queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        await bus.SendAsync<CreateRecipeCommand, CreateRecipeResult>(
            new CreateRecipeCommand
            {
                Title = "Filter Meat Lasagna Unique",
                Category = (int)RecipeCategory.Meat,
                PreparationTimeMinutes = 5,
                CookingTimeMinutes = 10,
                Ingredients =
                [
                    new CreateRecipeIngredientDto
                    {
                        Name = "Gehakt",
                        Quantity = 100,
                        Unit = Unit.Gram.ToString(),
                    },
                ],
                InstructionSteps =
                [
                    new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Cook." },
                ],
            });

        await bus.SendAsync<CreateRecipeCommand, CreateRecipeResult>(
            new CreateRecipeCommand
            {
                Title = "Filter Veggie Soup Unique",
                Category = (int)RecipeCategory.Vegetarian,
                PreparationTimeMinutes = 5,
                CookingTimeMinutes = 10,
                Ingredients =
                [
                    new CreateRecipeIngredientDto
                    {
                        Name = "Wortel",
                        Quantity = 2,
                        Unit = Unit.Piece.ToString(),
                    },
                ],
                InstructionSteps =
                [
                    new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Boil." },
                ],
            });

        var filtered = await queryBus.QueryAsync<GetRecipeListQuery, GetRecipeListResult>(
            new GetRecipeListQuery
            {
                Search = "Filter Meat Lasagna",
                Category = (int)RecipeCategory.Meat,
            });

        Assert.Contains(filtered.Items, r => r.Title == "Filter Meat Lasagna Unique");
        Assert.DoesNotContain(filtered.Items, r => r.Title == "Filter Veggie Soup Unique");
    }
}
