namespace RecipeLibrary.Domain.ValueObjects;

/// <summary>
/// Diet/category of a recipe for filtering (all, vegetarian, meat, vegan).
/// </summary>
public enum RecipeCategory
{
    Unknown = 0,
    Vegetarian,
    Meat,
    Vegan
}
