using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class AddRecipesToShoppingListCommandHandler(
    IRecipeRepository recipeRepository,
    IShoppingListRepository shoppingListRepository,
    IPantryRepository pantryRepository,
    ICurrentUser userContext,
    ShoppingListIngredientMerger merger,
    PantryExclusionFilter pantryExclusionFilter)
    : ICommandHandler<AddRecipesToShoppingListCommand, AddRecipesToShoppingListResult>
{
    public async Task<AddRecipesToShoppingListResult> HandleAsync(
        AddRecipesToShoppingListCommand command,
        CancellationToken ct = default)
    {
        if (command.RecipeIds.Count == 0)
        {
            throw new ArgumentException("At least one recipe id is required.");
        }

        await ShoppingListAccessGuard.EnsureListAccessAsync(
            shoppingListRepository,
            command.ShoppingListId,
            userContext.UserId,
            ct);

        var list = await shoppingListRepository.GetListByIdAsync(command.ShoppingListId, ct)
            ?? throw new InvalidOperationException("Shopping list not found.");

        var ownerUserId = userContext.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");
        var distinctIds = command.RecipeIds.Distinct().ToList();
        var recipes = await recipeRepository.GetByIdsAsync(ownerUserId, distinctIds, ct);
        if (recipes.Count != distinctIds.Count)
        {
            throw new UnauthorizedAccessException("One or more recipes are not accessible.");
        }

        var lines = new List<ShoppingListIngredientLine>();

        foreach (var recipe in recipes)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                lines.Add(new ShoppingListIngredientLine
                {
                    CanonicalIngredientId = ingredient.IngredientId,
                    DisplayName = ingredient.Name,
                    Preparation = ingredient.Preparation,
                    Quantity = ingredient.Quantity?.Value,
                    Unit = ingredient.Unit,
                    RecipeId = recipe.Id,
                    RecipeTitle = recipe.Title.Value,
                });
            }
        }

        var ownerKey = PantryOwnerKey.Resolve(userContext.UserId, list.GroupId);
        var pantryItems = await pantryRepository.GetByOwnerKeyAsync(ownerKey, ct);
        if (pantryItems.Count > 0)
        {
            lines = pantryExclusionFilter.ExcludeMatchingLines(lines, pantryItems).ToList();
        }

        var merged = merger.MergeIntoList(list.Items.ToList(), lines, list.Id);
        await shoppingListRepository.ReplaceListItemsAsync(list.Id, merged, ct);

        return new AddRecipesToShoppingListResult(recipes.Count, lines.Count);
    }
}
