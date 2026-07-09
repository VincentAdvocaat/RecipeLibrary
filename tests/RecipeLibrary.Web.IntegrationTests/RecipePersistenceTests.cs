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
}
