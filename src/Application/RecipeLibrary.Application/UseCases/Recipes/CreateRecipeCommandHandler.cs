using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Validators;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class CreateRecipeCommandHandler(IRecipeRepository recipeRepository)
    : ICommandHandler<CreateRecipeCommand, CreateRecipeResult>
{
    public async Task<CreateRecipeResult> HandleAsync(CreateRecipeCommand command, CancellationToken ct = default)
    {
        CreateRecipeCommandValidator.ValidateAndThrow(command);

        var title = (command.Title ?? string.Empty).Trim();
        var recipeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var recipe = new Recipe
        {
            Id = recipeId,
            Title = new RecipeTitle(title),
            Duration = new Duration(command.PreparationTimeMinutes),
            Difficulty = Difficulty.Unknown,
            Servings = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var ingredientDto in command.Ingredients)
        {
            var name = (ingredientDto!.Name ?? string.Empty).Trim();
            var unit = ParseUnitOrThrow(ingredientDto.Unit);

            recipe.Ingredients.Add(new Ingredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                Name = name,
                Quantity = new Quantity(ingredientDto.Quantity),
                Unit = unit,
            });
        }

        foreach (var stepDto in command.InstructionSteps.OrderBy(s => s!.StepNumber))
        {
            var text = (stepDto!.Text ?? string.Empty).Trim();

            recipe.InstructionSteps.Add(new InstructionStep
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                StepNumber = stepDto.StepNumber,
                Text = text,
            });
        }

        await recipeRepository.AddAsync(recipe, ct);
        return new CreateRecipeResult(recipeId);
    }

    private static Unit ParseUnitOrThrow(string? unit)
    {
        var raw = (unit ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("Ingredient unit is required.");
        }

        if (!Enum.TryParse<Unit>(raw, ignoreCase: true, out var parsed) || parsed == Unit.Unknown)
        {
            throw new ArgumentException($"Unknown unit '{raw}'. Use one of: {string.Join(", ", Enum.GetNames<Unit>().Where(n => n != nameof(Unit.Unknown)))}");
        }

        return parsed;
    }
}

