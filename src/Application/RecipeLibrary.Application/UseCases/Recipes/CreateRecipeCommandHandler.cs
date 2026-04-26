using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Validators;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class CreateRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository,
    IIngredientTextNormalizer normalizer,
    IngredientMatcher matcher,
    IngredientNameParser parser)
    : ICommandHandler<CreateRecipeCommand, CreateRecipeResult>
{
    public async Task<CreateRecipeResult> HandleAsync(CreateRecipeCommand command, CancellationToken ct = default)
    {
        CreateRecipeCommandValidator.ValidateAndThrow(command);

        var title = (command.Title ?? string.Empty).Trim();
        var recipeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var category = Enum.IsDefined(typeof(RecipeCategory), command.Category)
            ? (RecipeCategory)command.Category
            : RecipeCategory.Unknown;

        var description = (command.Description ?? string.Empty).Trim();
        var imageUrl = (command.ImageUrl ?? string.Empty).Trim();

        var recipe = new Recipe
        {
            Id = recipeId,
            Title = new RecipeTitle(title),
            Description = string.IsNullOrEmpty(description) ? null : description,
            ImageUrl = string.IsNullOrEmpty(imageUrl) ? null : imageUrl,
            PreparationMinutes = command.PreparationTimeMinutes,
            CookingMinutes = command.CookingTimeMinutes,
            Category = category,
            Difficulty = Difficulty.Unknown,
            Servings = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var ingredientDto in command.Ingredients)
        {
            var name = (ingredientDto!.Name ?? string.Empty).Trim();
            var unit = ParseUnitOrThrow(ingredientDto.Unit);
            var parsed = parser.ParseIngredient(name);
            var match = await matcher.MatchAsync(parsed.Name, ct);
            var canonicalIngredient = match.Ingredient;

            if (canonicalIngredient is null)
            {
                var normalized = normalizer.Normalize(parsed.Name);
                canonicalIngredient = await ingredientRepository.CreateIngredientWithAliasAsync(
                    parsed.Name,
                    normalized,
                    name,
                    normalizer.Normalize(name),
                    ct);
            }

            recipe.Ingredients.Add(new Ingredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                Name = name,
                Preparation = parsed.Preparation,
                IngredientId = canonicalIngredient.Id,
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

