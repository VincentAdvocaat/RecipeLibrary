namespace RecipeLibrary.Domain.ValueObjects;

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
    Piece,
    /// <summary>Garlic clove (teen).</summary>
    Clove,
    /// <summary>Handful (handje).</summary>
    Handful,
    /// <summary>Slice of bread etc. (sneetje / plakje).</summary>
    Slice,
    /// <summary>Herb sprig (takje).</summary>
    Sprig,
    /// <summary>Leaf (blaadje).</summary>
    Leaf,
    /// <summary>Bunch (bosje).</summary>
    Bunch,
    /// <summary>Stalk (stengel).</summary>
    Stalk,
    /// <summary>Measuring cup (cup / kopje).</summary>
    Cup,
    /// <summary>Weight ounce (oz).</summary>
    Ounce,
    /// <summary>Pound (lb / lbs).</summary>
    Pound,
    /// <summary>Can / tin (blik).</summary>
    Can,
}
