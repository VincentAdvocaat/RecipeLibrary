using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class AddRecipesToShoppingListCommandHandler(
    IRecipeRepository recipeRepository,
    IShoppingListRepository shoppingListRepository,
    ShoppingListIngredientMerger merger)
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

        var list = await shoppingListRepository.GetListByIdAsync(command.ShoppingListId, ct)
            ?? throw new InvalidOperationException("Shopping list not found.");

        var recipes = await recipeRepository.GetByIdsAsync(command.RecipeIds, ct);
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
                    Quantity = ingredient.Quantity.Value,
                    Unit = ingredient.Unit,
                    RecipeId = recipe.Id,
                    RecipeTitle = recipe.Title.Value,
                });
            }
        }

        var merged = merger.MergeIntoList(list.Items.ToList(), lines, list.Id);
        await shoppingListRepository.ReplaceListItemsAsync(list.Id, merged, ct);

        return new AddRecipesToShoppingListResult(recipes.Count, lines.Count);
    }
}
