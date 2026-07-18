namespace RecipeLibrary.Domain.ValueObjects;

/// <summary>
/// Classifies units by dimension and culinary treatment (kitchen measures vs standard physical units).
/// </summary>
public static class UnitClassification
{
    public static UnitDimension GetDimension(Unit unit) =>
        unit switch
        {
            Unit.Gram or Unit.Ounce or Unit.Pound => UnitDimension.Mass,
            Unit.Milliliter or Unit.Teaspoon or Unit.Tablespoon or Unit.Cup => UnitDimension.Volume,
            Unit.Piece or Unit.Clove or Unit.Handful or Unit.Slice or Unit.Sprig
                or Unit.Leaf or Unit.Bunch or Unit.Stalk or Unit.Can => UnitDimension.Count,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown unit dimension."),
        };

    /// <summary>
    /// Culinary volume units kept as-is for storage/presentation (not auto-normalized to ml/g).
    /// </summary>
    public static bool IsKitchenMeasure(Unit unit) =>
        unit is Unit.Teaspoon or Unit.Tablespoon or Unit.Cup;

    public static bool IsMass(Unit unit) => GetDimension(unit) == UnitDimension.Mass;

    public static bool IsStandardVolume(Unit unit) =>
        unit is Unit.Milliliter;
}
