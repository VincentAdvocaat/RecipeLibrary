using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class GetRecipeByIdQueryHandler(IRecipeRepository recipeRepository)
    : IQueryHandler<GetRecipeByIdQuery, GetRecipeByIdResult?>
{
    public async Task<GetRecipeByIdResult?> HandleAsync(GetRecipeByIdQuery query, CancellationToken ct = default)
    {
        var recipe = await recipeRepository.GetByIdAsync(query.RecipeId, ct);
        if (recipe is null)
        {
            return null;
        }

        return MapToDetailResult(recipe);
    }

    internal static GetRecipeByIdResult MapToDetailResult(Recipe recipe)
    {
        return new GetRecipeByIdResult
        {
            Id = recipe.Id,
            Title = recipe.Title.Value,
            Description = recipe.Description,
            ImageUrl = recipe.ImageUrl,
            PreparationMinutes = recipe.PreparationMinutes,
            CookingMinutes = recipe.CookingMinutes,
            Category = (int)recipe.Category,
            Servings = recipe.Servings,
            Difficulty = (int)recipe.Difficulty,
            Ingredients = recipe.Ingredients
                .OrderBy(i => i.Name)
                .Select(i => new RecipeDetailIngredientItem
                {
                    Name = i.Name,
                    Preparation = i.Preparation,
                    Quantity = i.Quantity.Value,
                    Unit = i.Unit.ToString(),
                })
                .ToList(),
            Steps = recipe.InstructionSteps
                .OrderBy(s => s.StepNumber)
                .Select(s => new RecipeDetailStepItem
                {
                    StepNumber = s.StepNumber,
                    Text = s.Text,
                })
                .ToList(),
        };
    }
}
