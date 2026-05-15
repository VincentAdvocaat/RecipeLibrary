using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class GetRecipeIngredientTagsQueryHandler(IRecipeRepository recipeRepository)
    : IQueryHandler<GetRecipeIngredientTagsQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> HandleAsync(GetRecipeIngredientTagsQuery query, CancellationToken ct = default)
        => recipeRepository.GetIngredientTagNamesForRecipeAsync(query.RecipeId, ct);
}
