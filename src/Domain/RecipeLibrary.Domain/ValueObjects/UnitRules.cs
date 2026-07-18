namespace RecipeLibrary.Domain.ValueObjects;

/// <summary>
/// Display and validation rules per <see cref="Unit"/>.
/// </summary>
public static class UnitRules
{
    public static IReadOnlyList<string> SelectableUnitNames { get; } =
        Enum.GetNames<Unit>()
            .Where(n => n != nameof(Unit.Unknown))
            .ToArray();

    /// <summary>
    /// Units offered in create/edit dropdowns for the given measure preference.
    /// Non-preferred mass units are hidden unless they are the current selection
    /// (so imported imperial lines remain editable under a metric preference, and vice versa).
    /// </summary>
    public static IReadOnlyList<string> SelectableUnitNamesFor(
        MeasureSystem measureSystem,
        string? currentUnitName = null)
    {
        Unit? currentUnit = null;
        if (!string.IsNullOrWhiteSpace(currentUnitName)
            && TryParse(currentUnitName, out var parsed)
            && parsed != Unit.Unknown)
        {
            currentUnit = parsed;
        }

        return SelectableUnitNames
            .Where(name =>
            {
                if (!TryParse(name, out var unit) || unit == Unit.Unknown)
                {
                    return false;
                }

                if (!UnitClassification.IsMass(unit))
                {
                    return true;
                }

                if (currentUnit is { } current && unit == current)
                {
                    return true;
                }

                return measureSystem == MeasureSystem.Metric
                    ? unit == Unit.Gram
                    : unit is Unit.Ounce or Unit.Pound;
            })
            .ToArray();
    }

    /// <summary>
    /// Count-style units, including culinary piece descriptors and cans.
    /// </summary>
    public static bool IsCountUnit(Unit unit) =>
        unit is Unit.Piece
            or Unit.Clove
            or Unit.Handful
            or Unit.Slice
            or Unit.Sprig
            or Unit.Leaf
            or Unit.Bunch
            or Unit.Stalk
            or Unit.Can;

    /// <summary>
    /// Units that allow discrete culinary fractions (¼, ⅓, ½, ⅔, ¾).
    /// Prefer these over converting cup/piece measures into ml/gram.
    /// Ounce is mass (decimals), not a culinary-fraction unit.
    /// </summary>
    public static bool AllowsCulinaryFractions(Unit unit) =>
        unit is Unit.Teaspoon
            or Unit.Tablespoon
            or Unit.Cup
            or Unit.Piece
            or Unit.Clove;

    /// <summary>
    /// Continuous decimal quantities (not snapped to whole numbers or culinary fractions).
    /// Ounce/Pound allow values like 1.5 oz; gram/ml stay whole numbers for input.
    /// </summary>
    public static bool AllowsDecimalQuantity(Unit unit) =>
        unit is Unit.Pound or Unit.Ounce;

    /// <summary>
    /// Smallest culinary fractional step (¼). Thirds are also allowed via the fraction select.
    /// </summary>
    public static decimal InputStep(Unit unit) =>
        AllowsCulinaryFractions(unit) ? 0.25m : AllowsDecimalQuantity(unit) ? 0.01m : 1m;

    public static bool TryParse(string? value, out Unit unit)
    {
        var raw = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            unit = Unit.Unknown;
            return false;
        }

        if (Enum.TryParse<Unit>(raw, ignoreCase: true, out unit) && unit != Unit.Unknown)
        {
            return true;
        }

        if (UnitAliases.TryResolve(raw, out unit) && unit != Unit.Unknown)
        {
            return true;
        }

        unit = Unit.Unknown;
        return false;
    }

    public static Unit ParseOrThrow(string? value)
    {
        if (!TryParse(value, out var unit))
        {
            throw new ArgumentException(
                $"Unknown unit '{value?.Trim()}'. Use one of: {string.Join(", ", SelectableUnitNames)}");
        }

        return unit;
    }
}
