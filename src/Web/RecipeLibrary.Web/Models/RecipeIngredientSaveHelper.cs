using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Web.Models;

public static class RecipeIngredientSaveHelper
{
    public static List<CreateRecipeIngredientDto> ToIngredientDtos(IEnumerable<RecipeIngredientEditorItem> ingredients) =>
        ingredients
            .Where(static x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x =>
            {
                var unmeasured = x.IsUnmeasured;
                return new CreateRecipeIngredientDto
                {
                    Name = x.Name.Trim(),
                    Preparation = string.IsNullOrWhiteSpace(x.Preparation) ? null : x.Preparation.Trim(),
                    Quantity = unmeasured ? null : x.Quantity,
                    Unit = unmeasured ? null : x.UnitName,
                };
            }).ToList();
}
