using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Ingredients;

public sealed class SearchIngredientsQueryHandler(IIngredientRepository ingredientRepository, IIngredientTextNormalizer normalizer)
    : IQueryHandler<SearchIngredientsQuery, IReadOnlyList<IngredientLookupItem>>
{
    public async Task<IReadOnlyList<IngredientLookupItem>> HandleAsync(SearchIngredientsQuery query, CancellationToken ct = default)
    {
        var normalizedQuery = normalizer.Normalize(query.Query);
        var items = await ingredientRepository.SearchAsync(normalizedQuery, 10, ct);
        return items.Select(x => new IngredientLookupItem { Id = x.Id, Name = x.CanonicalName }).ToList();
    }
}
