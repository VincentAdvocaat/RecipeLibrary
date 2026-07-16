using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.RecipeImport;

public static class ImportRecipeResultMapper
{
    public static CreateRecipeCommand ToCreateRecipeCommand(ImportRecipeResult result) =>
        new()
        {
            Title = result.Title ?? string.Empty,
            Description = result.Description,
            PreparationTimeMinutes = result.PreparationTimeMinutes ?? 0,
            CookingTimeMinutes = result.CookingTimeMinutes ?? 0,
            Difficulty = result.Difficulty ?? 0,
            Category = result.Category ?? 0,
            Servings = result.Servings ?? 0,
            ImageUrl = null,
            Ingredients = result.Ingredients
                .Select(i => new CreateRecipeIngredientDto
                {
                    Name = i.Name,
                    Preparation = i.Preparation,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    CreateAsNewIngredient = false,
                })
                .ToList(),
            InstructionSteps = result.Steps
                .Select(s => new CreateRecipeInstructionStepDto
                {
                    StepNumber = s.StepNumber,
                    Text = s.Text,
                })
                .ToList(),
        };
}
