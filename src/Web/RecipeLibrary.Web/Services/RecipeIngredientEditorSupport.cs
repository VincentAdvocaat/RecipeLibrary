using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Web.Models;

namespace RecipeLibrary.Web.Services;

internal static class RecipeIngredientEditorSupport
{
    internal static async Task ApplyIngredientTagsAsync(
        ICommandBus commandBus,
        IEnumerable<RecipeIngredientEditorItem> ingredients,
        IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            return;
        }

        foreach (var ingredient in ingredients)
        {
            var input = ingredient.Name.Trim();
            if (input.Length == 0)
            {
                continue;
            }

            var payload = await commandBus.SendAsync<MatchIngredientCommand, MatchIngredientResult>(
                IngredientApi.MatchCommand(input));
            if (payload.Ingredient?.Id is not Guid ingredientId)
            {
                continue;
            }

            await commandBus.SendAsync<AddIngredientTagsCommand, AddIngredientTagsResult>(
                new AddIngredientTagsCommand
                {
                    IngredientId = ingredientId,
                    Tags = tags,
                });
        }
    }
}
