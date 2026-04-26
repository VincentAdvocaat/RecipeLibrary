using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Ingredients;

public sealed class SearchTagsQueryHandler(IIngredientRepository ingredientRepository, IIngredientTextNormalizer normalizer)
    : IQueryHandler<SearchTagsQuery, IReadOnlyList<TagLookupItem>>
{
    public async Task<IReadOnlyList<TagLookupItem>> HandleAsync(SearchTagsQuery query, CancellationToken ct = default)
    {
        var normalized = normalizer.Normalize(query.Query);
        var tags = await ingredientRepository.SearchTagsAsync(normalized, 10, ct);
        return tags.Select(x => new TagLookupItem { Id = x.Id, Name = x.Name }).ToList();
    }
}
