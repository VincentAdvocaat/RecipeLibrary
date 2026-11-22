using Microsoft.Extensions.Localization;
using RecipeLibrary.Resources;
using RecipeLibrary.Web.Models;

namespace RecipeLibrary.Web.Services;

internal static class RecipeEditorValidation
{
    /// <summary>
    /// Localized required-field checks. Prefer this over [Required] with SharedResources
    /// (marker type has no static resource properties and would crash the Blazor circuit).
    /// </summary>
    public static string? GetRequiredFieldError(
        IStringLocalizer<SharedResources> localizer,
        string? title,
        IEnumerable<RecipeIngredientEditorItem> ingredients)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return localizer["RecipeCreate.Title.Required"];
        }

        if (ingredients.All(static i => string.IsNullOrWhiteSpace(i.Name)))
        {
            return localizer["RecipeCreate.Ingredient.Name.Required"];
        }

        return null;
    }
}
