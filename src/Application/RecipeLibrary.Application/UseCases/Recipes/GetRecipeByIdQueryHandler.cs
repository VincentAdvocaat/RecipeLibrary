using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class GetRecipeByIdQueryHandler(
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser)
    : IQueryHandler<GetRecipeByIdQuery, GetRecipeByIdResult?>
{
    public async Task<GetRecipeByIdResult?> HandleAsync(GetRecipeByIdQuery query, CancellationToken ct = default)
    {
        var ownerUserId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        var recipe = await recipeRepository.GetByIdAsync(ownerUserId, query.RecipeId, ct);
        if (recipe is null)
        {
            return null;
        }

        return MapToDetailResult(recipe, ownerUserId);
    }

    private static GetRecipeByIdResult MapToDetailResult(Recipe recipe, string userId)
    {
        return new GetRecipeByIdResult
        {
            Id = recipe.Id,
            HouseholdId = recipe.HouseholdId,
            Title = recipe.Title.Value,
            Description = recipe.Description,
            SourceUrl = recipe.SourceUrl,
            ImageUrl = recipe.ImageUrl,
            PreparationMinutes = recipe.PreparationMinutes,
            CookingMinutes = recipe.CookingMinutes,
            Category = (int)recipe.Category,
            Servings = recipe.Servings,
            Difficulty = (int)recipe.Difficulty,
            MyRating = recipe.Ratings.FirstOrDefault(r => r.UserId == userId)?.Stars,
            Ingredients = recipe.Ingredients
                .OrderBy(i => i.Name)
                .Select(i => new RecipeDetailIngredientItem
                {
                    CanonicalIngredientId = i.IngredientId,
                    Name = i.Name,
                    Preparation = i.Preparation,
                    Quantity = i.Quantity?.Value,
                    Unit = i.Unit?.ToString(),
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
