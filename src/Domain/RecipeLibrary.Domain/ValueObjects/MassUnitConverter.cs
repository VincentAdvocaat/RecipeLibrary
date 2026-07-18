namespace RecipeLibrary.Domain.ValueObjects;

/// <summary>
/// Exact Mass↔Mass conversion helpers for display (not ingredient-density conversions).
/// </summary>
public static class MassUnitConverter
{
    public const decimal GramsPerOunce = 28.349523125m;
    public const decimal GramsPerPound = 453.59237m;

    public static decimal ToGrams(decimal quantity, Unit unit) =>
        unit switch
        {
            Unit.Gram => quantity,
            Unit.Ounce => quantity * GramsPerOunce,
            Unit.Pound => quantity * GramsPerPound,
            _ => throw new ArgumentException($"Unit '{unit}' is not a mass unit.", nameof(unit)),
        };

    public static decimal FromGrams(decimal grams, Unit targetUnit) =>
        targetUnit switch
        {
            Unit.Gram => grams,
            Unit.Ounce => grams / GramsPerOunce,
            Unit.Pound => grams / GramsPerPound,
            _ => throw new ArgumentException($"Unit '{targetUnit}' is not a mass unit.", nameof(targetUnit)),
        };
}
