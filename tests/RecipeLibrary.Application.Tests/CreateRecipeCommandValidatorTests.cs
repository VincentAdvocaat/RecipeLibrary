using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Validators;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class CreateRecipeCommandValidatorTests
{
    [Fact]
    public void Validate_Throws_WhenTitleEmpty()
    {
        var command = ValidCommand(title: "  ");

        var ex = Assert.Throws<ArgumentException>(() => CreateRecipeCommandValidator.ValidateAndThrow(command));
        Assert.Contains("Title", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AcceptsIngredientWithPreparation()
    {
        var command = ValidCommand(preparation: "ruim");

        CreateRecipeCommandValidator.ValidateAndThrow(command);
    }

    private static CreateRecipeCommand ValidCommand(string title = "Test Recipe", string? preparation = null) => new()
    {
        Title = title,
        Category = 2,
        PreparationTimeMinutes = 10,
        CookingTimeMinutes = 20,
        Ingredients =
        [
            new CreateRecipeIngredientDto
            {
                Name = "Gehakt",
                Preparation = preparation,
                Quantity = 500,
                Unit = Unit.Gram.ToString(),
            },
        ],
        InstructionSteps =
        [
            new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Cook." },
        ],
    };
}
