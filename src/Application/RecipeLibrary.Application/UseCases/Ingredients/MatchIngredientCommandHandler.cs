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
        var result = await matcher.MatchAsync(rawInput, ct);

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
            Ingredient = result.Ingredient is null ? null : new IngredientLookupItem { Id = result.Ingredient.Id, Name = result.Ingredient.CanonicalName },
            Suggestions = result.Suggestions.Select(x => new IngredientLookupItem { Id = x.Id, Name = x.CanonicalName }).ToList()
        };
    }
}
