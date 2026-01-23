using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class CreateRecipeService(IRecipeRepository recipeRepository) : IRecipeService
{
    public async Task<Guid> CreateAsync(CreateRecipeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(request));
        }

        if (request.PreparationTimeMinutes <= 0)
        {
            throw new ArgumentException("Preparation time must be greater than 0 minutes.", nameof(request));
        }

        if (request.Ingredients is null || request.Ingredients.Count == 0)
        {
            throw new ArgumentException("At least one ingredient is required.", nameof(request));
        }

        if (request.InstructionSteps is null || request.InstructionSteps.Count == 0)
        {
            throw new ArgumentException("At least one instruction step is required.", nameof(request));
        }

        var recipeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var recipe = new Recipe
        {
            Id = recipeId,
            Title = new RecipeTitle(title),
            Duration = new Duration(request.PreparationTimeMinutes),
            Difficulty = Difficulty.Unknown,
            Servings = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var ingredientDto in request.Ingredients)
        {
            ArgumentNullException.ThrowIfNull(ingredientDto);

            var name = (ingredientDto.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Ingredient name is required.", nameof(request));
            }

            if (ingredientDto.Quantity <= 0)
            {
                throw new ArgumentException("Ingredient quantity must be greater than 0.", nameof(request));
            }

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

        foreach (var stepDto in request.InstructionSteps.OrderBy(s => s.StepNumber))
        {
            if (stepDto is null)
            {
                continue;
            }

            if (stepDto.StepNumber <= 0)
            {
                throw new ArgumentException("StepNumber must be >= 1.", nameof(request));
            }

            var text = (stepDto.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Instruction step text is required.", nameof(request));
            }

            recipe.InstructionSteps.Add(new InstructionStep
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                StepNumber = stepDto.StepNumber,
                Text = text,
            });
        }

        await recipeRepository.AddAsync(recipe, ct);
        return recipeId;
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

