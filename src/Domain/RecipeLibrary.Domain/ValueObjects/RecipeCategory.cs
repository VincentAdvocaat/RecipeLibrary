namespace RecipeLibrary.Domain.ValueObjects;

/// <summary>
/// Diet/category of a recipe for filtering (Alle, Vegetarisch, Vlees, Vegan).
/// </summary>
public enum RecipeCategory
{
    Unknown = 0,
    Vegetarian,
    Meat,
    Vegan
}
