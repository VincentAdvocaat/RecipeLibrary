using System.ComponentModel.DataAnnotations;
using RecipeLibrary.Domain.ValueObjects;
using RecipeLibrary.Resources;

namespace RecipeLibrary.Web.Models;

public sealed class RecipeIngredientEditorItem
{
    [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = "RecipeCreate.Ingredient.Name.Required")]
    public string Name { get; set; } = string.Empty;

    public string? Preparation { get; set; }

    [Range(typeof(decimal), "0.0001", "1000000", ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = "RecipeCreate.Ingredient.Quantity.Invalid")]
    public decimal Quantity { get; set; } = 1;

    [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = "RecipeCreate.Ingredient.Unit.Required")]
    public string UnitName { get; set; } = nameof(Unit.Gram);
}
