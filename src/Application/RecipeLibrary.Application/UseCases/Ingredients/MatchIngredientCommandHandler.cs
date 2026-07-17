using System.Globalization;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.UseCases.Ingredients;

public sealed class MatchIngredientCommandHandler(
    IngredientMatcher matcher,
    IIngredientRepository ingredientRepository)
    : ICommandHandler<MatchIngredientCommand, MatchIngredientResult>
{
    public async Task<MatchIngredientResult> HandleAsync(MatchIngredientCommand command, CancellationToken ct = default)
    {
        var rawInput = (command.Input ?? string.Empty).Trim();
        var cultureName = CultureInfo.CurrentUICulture.Name;
        var result = await matcher.MatchAsync(rawInput, cultureName, ct);

        await ingredientRepository.AddMatchLogAsync(new IngredientMatchLog
        {
            Id = Guid.NewGuid(),
            Input = rawInput,
            NormalizedInput = result.NormalizedInput,
            MatchedIngredientId = result.Ingredient?.Id,
            MatchType = result.MatchType,
            Confidence = result.Confidence,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        return new MatchIngredientResult
        {
            MatchType = result.MatchType,
            Confidence = result.Confidence,
            RequiresConfirmation = result.RequiresConfirmation,
            Ingredient = result.Ingredient is null
                ? null
                : ToLookupItem(result.Ingredient, result.LanguageChain),
            Suggestions = result.Suggestions
                .Select(x => new IngredientSuggestionItem
                {
                    Id = x.Ingredient.Id,
                    Name = x.Display.DisplayName,
                    LanguageCode = x.Display.LanguageCode,
                    Score = x.Score,
                })
                .ToList(),
        };
    }

    private static IngredientLookupItem ToLookupItem(
        CanonicalIngredient ingredient,
        IReadOnlyList<string> languageChain)
    {
        var display = IngredientDisplayResolver.Resolve(ingredient, languageChain);
        return new IngredientLookupItem
        {
            Id = ingredient.Id,
            Name = display.DisplayName,
            LanguageCode = display.LanguageCode,
        };
    }
}
