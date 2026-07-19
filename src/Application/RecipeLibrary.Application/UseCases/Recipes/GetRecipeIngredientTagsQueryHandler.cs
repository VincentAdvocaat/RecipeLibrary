using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class GetRecipeIngredientTagsQueryHandler(
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser)
    : IQueryHandler<GetRecipeIngredientTagsQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> HandleAsync(GetRecipeIngredientTagsQuery query, CancellationToken ct = default)
    {
        var ownerUserId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        return recipeRepository.GetIngredientTagNamesForRecipeAsync(ownerUserId, query.RecipeId, ct);
    }
}
