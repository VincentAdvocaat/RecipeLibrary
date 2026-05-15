using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.UseCases.Recipes;

internal static class RecipeIngredientBuilder
{
    public static async Task<IReadOnlyList<Ingredient>> BuildAsync(
        Guid recipeId,
        IReadOnlyList<CreateRecipeIngredientDto> ingredientDtos,
        IIngredientRepository ingredientRepository,
        IIngredientTextNormalizer normalizer,
        IngredientMatcher matcher,
        IngredientNameParser parser,
        CancellationToken ct)
    {
        var ingredients = new List<Ingredient>();

        foreach (var ingredientDto in ingredientDtos)
        {
            var name = (ingredientDto.Name ?? string.Empty).Trim();
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

            ingredients.Add(new Ingredient
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

        return ingredients;
    }

    public static IReadOnlyList<InstructionStep> BuildSteps(Guid recipeId, IReadOnlyList<CreateRecipeInstructionStepDto> stepDtos)
    {
        return stepDtos
            .OrderBy(s => s.StepNumber)
            .Select(stepDto =>
            {
                var text = (stepDto.Text ?? string.Empty).Trim();
                return new InstructionStep
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipeId,
                    StepNumber = stepDto.StepNumber,
                    Text = text,
                };
            })
            .ToList();
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
