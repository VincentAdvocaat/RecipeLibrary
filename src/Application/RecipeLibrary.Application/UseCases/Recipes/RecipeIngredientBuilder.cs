using System.Globalization;
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
        IngredientLineResolver lineResolver,
        CancellationToken ct)
    {
        var ingredients = new List<Ingredient>();
        var cultureName = CultureInfo.CurrentUICulture.Name;
        var languageCode = IngredientLanguageFallback.ResolveStorageLanguageCode(cultureName);

        foreach (var ingredientDto in ingredientDtos)
        {
            var rawName = (ingredientDto.Name ?? string.Empty).Trim();
            var resolved = lineResolver.Resolve(ingredientDto.Name, ingredientDto.Preparation);
            CanonicalIngredient canonicalIngredient;

            if (ingredientDto.CreateAsNewIngredient)
            {
                var normalized = normalizer.Normalize(resolved.DisplayName);
                canonicalIngredient = await ingredientRepository.FindOrCreateAsync(
                    languageCode,
                    resolved.DisplayName,
                    normalized,
                    rawName,
                    normalizer.Normalize(rawName),
                    ct);
            }
            else
            {
                var match = await matcher.MatchAsync(resolved.DisplayName, cultureName, ct);
                canonicalIngredient = match.Ingredient
                    ?? await FindOrCreateNewIngredientAsync(
                        ingredientRepository,
                        normalizer,
                        languageCode,
                        resolved.DisplayName,
                        rawName,
                        ct);
            }

            Quantity? quantity = null;
            Unit? unit = null;
            if (!IngredientMeasure.IsUnmeasured(ingredientDto.Quantity, ingredientDto.Unit))
            {
                unit = UnitRules.ParseOrThrow(ingredientDto.Unit);
                quantity = new Quantity(
                    IngredientQuantityFormatter.Normalize(ingredientDto.Quantity!.Value, unit.Value));
            }

            ingredients.Add(new Ingredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                Name = resolved.DisplayName,
                Preparation = resolved.Preparation,
                IngredientId = canonicalIngredient.Id,
                Quantity = quantity,
                Unit = unit,
            });
        }

        return ingredients;
    }

    private static Task<CanonicalIngredient> FindOrCreateNewIngredientAsync(
        IIngredientRepository ingredientRepository,
        IIngredientTextNormalizer normalizer,
        string languageCode,
        string displayName,
        string rawName,
        CancellationToken ct)
    {
        var normalized = normalizer.Normalize(displayName);
        return ingredientRepository.FindOrCreateAsync(
            languageCode,
            displayName,
            normalized,
            rawName,
            normalizer.Normalize(rawName),
            ct);
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
}
