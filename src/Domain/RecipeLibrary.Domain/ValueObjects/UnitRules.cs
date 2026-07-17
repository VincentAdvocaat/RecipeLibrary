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
    /// </summary>
    public static bool AllowsCulinaryFractions(Unit unit) =>
        unit is Unit.Teaspoon
            or Unit.Tablespoon
            or Unit.Cup
            or Unit.Piece
            or Unit.Clove
            or Unit.Ounce;

    /// <summary>
    /// Continuous decimal quantities (not snapped to whole numbers or culinary fractions).
    /// Pound keeps values like 1.3 lbs; gram/ml stay whole numbers.
    /// </summary>
    public static bool AllowsDecimalQuantity(Unit unit) =>
        unit is Unit.Pound;

    /// <summary>
    /// Smallest culinary fractional step (¼). Thirds are also allowed via the fraction select.
    /// </summary>
    public static decimal InputStep(Unit unit) =>
        AllowsCulinaryFractions(unit) ? 0.25m : unit is Unit.Pound ? 0.01m : 1m;

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
