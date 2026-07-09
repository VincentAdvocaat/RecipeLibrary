using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.ValueObjects;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class UpdateRecipePersistenceTests(SqlContainerFixture fixture)
{
    [Fact]
    public async Task UpdateRecipe_ChangesTitleAndPreparation()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        var queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        var create = await bus.SendAsync<CreateRecipeCommand, CreateRecipeResult>(
            new CreateRecipeCommand
            {
                Title = "Before Update",
                Category = 2,
                PreparationTimeMinutes = 5,
                CookingTimeMinutes = 10,
                Ingredients =
                [
                    new CreateRecipeIngredientDto
                    {
                        Name = "Gehakt",
                        Preparation = "fijn",
                        Quantity = 300,
                        Unit = Unit.Gram.ToString(),
                    },
                ],
                InstructionSteps =
                [
                    new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Mix." },
                ],
            });

        await bus.SendAsync<UpdateRecipeCommand, UpdateRecipeResult>(
            new UpdateRecipeCommand
            {
                RecipeId = create.RecipeId,
                Title = "After Update",
                Category = 2,
                PreparationTimeMinutes = 5,
                CookingTimeMinutes = 10,
                Ingredients =
                [
                    new CreateRecipeIngredientDto
                    {
                        Name = "Gehakt",
                        Preparation = "ruim",
                        Quantity = 400,
                        Unit = Unit.Gram.ToString(),
                    },
                ],
                InstructionSteps =
                [
                    new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Mix again." },
                ],
            });

        var loaded = await queryBus.QueryAsync<GetRecipeByIdQuery, GetRecipeByIdResult?>(
            new GetRecipeByIdQuery { RecipeId = create.RecipeId });

        Assert.NotNull(loaded);
        Assert.Equal("After Update", loaded.Title);
        Assert.Equal("ruim", loaded.Ingredients[0].Preparation);
        Assert.Equal(400, loaded.Ingredients[0].Quantity);
    }
}
