using System.ComponentModel.DataAnnotations;
using RecipeLibrary.Domain.ValueObjects;
using RecipeLibrary.Resources;

namespace RecipeLibrary.Web.Models;

public sealed class RecipeIngredientEditorItem
{
    [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = "RecipeCreate.Ingredient.Name.Required")]
    public string Name { get; set; } = string.Empty;

    public string? Preparation { get; set; }

    /// <summary>Null when unmeasured (e.g. naar smaak).</summary>
    public decimal? Quantity { get; set; } = 1;

    /// <summary>Null when unmeasured (e.g. naar smaak).</summary>
    public string? UnitName { get; set; } = nameof(Unit.Gram);

    /// <summary>True when import confidence was low; show a friendly review hint in the UI.</summary>
    public bool NeedsReview { get; set; }

    public bool IsUnmeasured =>
        string.IsNullOrWhiteSpace(UnitName) && (Quantity is null or <= 0);
}
