using System.Globalization;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;

namespace RecipeLibrary.Application.UseCases.Ingredients;

public sealed class SearchIngredientsQueryHandler(
    IIngredientRepository ingredientRepository,
    IIngredientTextNormalizer normalizer)
    : IQueryHandler<SearchIngredientsQuery, IReadOnlyList<IngredientLookupItem>>
{
    public async Task<IReadOnlyList<IngredientLookupItem>> HandleAsync(SearchIngredientsQuery query, CancellationToken ct = default)
    {
        var cultureName = string.IsNullOrWhiteSpace(query.CultureName)
            ? CultureInfo.CurrentUICulture.Name
            : query.CultureName;
        var languageChain = IngredientLanguageFallback.ResolveChain(cultureName);
        var normalizedQuery = normalizer.Normalize(query.Query);
        var items = await ingredientRepository.SearchAsync(normalizedQuery, languageChain, 10, ct);
        return items.Select(x =>
        {
            var display = IngredientDisplayResolver.Resolve(x, languageChain);
            return new IngredientLookupItem
            {
                Id = x.Id,
                Name = display.DisplayName,
                LanguageCode = display.LanguageCode,
            };
        }).ToList();
    }
}
