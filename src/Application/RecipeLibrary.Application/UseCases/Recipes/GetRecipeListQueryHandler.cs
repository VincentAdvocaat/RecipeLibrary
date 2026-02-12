using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class GetRecipeListQueryHandler(IRecipeRepository recipeRepository)
    : IQueryHandler<GetRecipeListQuery, GetRecipeListResult>
{
    public async Task<GetRecipeListResult> HandleAsync(GetRecipeListQuery query, CancellationToken ct = default)
    {
        RecipeCategory? category = null;
        if (query.Category is { } value && Enum.IsDefined(typeof(RecipeCategory), value))
        {
            category = (RecipeCategory)value;
        }

        var recipes = await recipeRepository.GetListAsync(query.Search, category, ct);
        var items = recipes.Select(MapToOverviewItem).ToList();
        return new GetRecipeListResult(items);
    }

    private static RecipeOverviewItem MapToOverviewItem(Recipe recipe)
    {
        return new RecipeOverviewItem
        {
            Id = recipe.Id,
            Title = recipe.Title.Value,
            Description = recipe.Description,
            ImageUrl = recipe.ImageUrl,
            PreparationMinutes = recipe.PreparationMinutes,
            CookingMinutes = recipe.CookingMinutes,
            Category = (int)recipe.Category,
            IngredientNames = recipe.Ingredients.OrderBy(i => i.Name).Select(i => i.Name).ToList(),
        };
    }
}
