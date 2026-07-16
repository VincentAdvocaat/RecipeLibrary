using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Validators;

public static class CreateRecipeCommandValidator
{
    public static void ValidateAndThrow(CreateRecipeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var title = (command.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(command));
        }

        if (command.PreparationTimeMinutes < 0)
        {
            throw new ArgumentException("Preparation time cannot be negative.", nameof(command));
        }

        if (command.CookingTimeMinutes < 0)
        {
            throw new ArgumentException("Cooking time cannot be negative.", nameof(command));
        }

        if (command.Servings < 0)
        {
            throw new ArgumentException("Servings cannot be negative.", nameof(command));
        }

        if (command.Servings > 100)
        {
            throw new ArgumentException("Servings cannot exceed 100.", nameof(command));
        }

        if (!Enum.IsDefined(typeof(Difficulty), command.Difficulty))
        {
            throw new ArgumentException("Difficulty is not a valid value.", nameof(command));
        }

        if (command.Ingredients is null || command.Ingredients.Count == 0)
        {
            throw new ArgumentException("At least one ingredient is required.", nameof(command));
        }

        if (command.Ingredients.Any(i => i is null))
        {
            throw new ArgumentException("Ingredients cannot contain null items.", nameof(command));
        }

        foreach (var ingredient in command.Ingredients)
        {
            var name = (ingredient!.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Ingredient name is required.", nameof(command));
            }

            if (IngredientMeasure.IsUnmeasured(ingredient.Quantity, ingredient.Unit))
            {
                if (ingredient.Preparation is { Length: > 200 })
                {
                    throw new ArgumentException("Ingredient preparation cannot exceed 200 characters.", nameof(command));
                }

                continue;
            }

            if (ingredient.Quantity is null || string.IsNullOrWhiteSpace(ingredient.Unit))
            {
                throw new ArgumentException(
                    "Ingredient quantity and unit are both required unless the ingredient is unmeasured.",
                    nameof(command));
            }

            var unitRaw = ingredient.Unit.Trim();
            if (!UnitRules.TryParse(unitRaw, out var unit))
            {
                throw new ArgumentException("Ingredient unit is required or not recognized.", nameof(command));
            }

            IngredientQuantityFormatter.ValidateQuantity(ingredient.Quantity.Value, unit);

            if (ingredient.Preparation is { Length: > 200 })
            {
                throw new ArgumentException("Ingredient preparation cannot exceed 200 characters.", nameof(command));
            }
        }

        if (command.InstructionSteps is null || command.InstructionSteps.Count == 0)
        {
            throw new ArgumentException("At least one instruction step is required.", nameof(command));
        }

        if (command.InstructionSteps.Any(s => s is null))
        {
            throw new ArgumentException("InstructionSteps cannot contain null items.", nameof(command));
        }

        foreach (var step in command.InstructionSteps)
        {
            if (step!.StepNumber <= 0)
            {
                throw new ArgumentException("StepNumber must be >= 1.", nameof(command));
            }

            var text = (step.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Instruction step text is required.", nameof(command));
            }
        }
    }
}
