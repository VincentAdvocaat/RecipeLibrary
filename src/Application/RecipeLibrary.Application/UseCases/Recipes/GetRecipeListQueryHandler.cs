using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class GetRecipeListQueryHandler(
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser,
    IHouseholdRecipeRepository? householdRecipes = null)
    : IQueryHandler<GetRecipeListQuery, GetRecipeListResult>
{
    public async Task<GetRecipeListResult> HandleAsync(GetRecipeListQuery query, CancellationToken ct = default)
    {
        var ownerUserId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        RecipeCategory? category = null;
        if (query.Category is { } value && Enum.IsDefined(typeof(RecipeCategory), value))
        {
            category = (RecipeCategory)value;
        }

        var recipes = query.HouseholdId is { } householdId
            ? await RequireHouseholdRepository().GetHouseholdListAsync(ownerUserId, householdId, query.Search, category, ct)
            : await recipeRepository.GetListAsync(ownerUserId, query.Search, category, ct);
        var sharedIds = query.SharingTargetHouseholdId is { } target
            ? await RequireHouseholdRepository().GetSharedSourceIdsAsync(ownerUserId, target, ct)
            : null;
        var items = recipes.Select(recipe => MapToOverviewItem(recipe, sharedIds?.Contains(recipe.Id) == true)).ToList();
        return new GetRecipeListResult(items);
    }

    private IHouseholdRecipeRepository RequireHouseholdRepository() =>
        householdRecipes ?? throw new InvalidOperationException("Household recipe storage is unavailable.");

    private static RecipeOverviewItem MapToOverviewItem(Recipe recipe, bool alreadyShared)
    {
        return new RecipeOverviewItem
        {
            Id = recipe.Id,
            AlreadyShared = alreadyShared,
            Title = recipe.Title.Value,
            Description = recipe.Description,
            ImageUrl = recipe.ImageUrl,
            PreparationMinutes = recipe.PreparationMinutes,
            CookingMinutes = recipe.CookingMinutes,
            Category = (int)recipe.Category,
            Servings = recipe.Servings,
            Difficulty = (int)recipe.Difficulty,
            IngredientNames = recipe.Ingredients
                .OrderBy(i => i.Name)
                .Select(i => IngredientListDisplay.FormatNameWithPreparation(i.Name, i.Preparation))
                .ToList(),
        };
    }
}
