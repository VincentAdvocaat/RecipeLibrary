namespace RecipeLibrary.Domain.ValueObjects;

/// <summary>
/// Strongly typed title for a recipe.
/// </summary>
public readonly record struct RecipeTitle(string Value);

/// <summary>
/// Represents a duration in minutes.
/// </summary>
public readonly record struct Duration(int Minutes);

/// <summary>
/// Quantity of an ingredient.
/// </summary>
public readonly record struct Quantity(decimal Value);

/// <summary>
/// Units that can be used for ingredient quantities.
/// </summary>
public enum Unit
{
    Unknown = 0,
    Gram,
    Milliliter,
    Teaspoon,
    Tablespoon,
    Piece
}

/// <summary>
/// Overall difficulty level of preparing a recipe.
/// </summary>
public enum Difficulty
{
    Unknown = 0,
    Easy,
    Medium,
    Hard
}

