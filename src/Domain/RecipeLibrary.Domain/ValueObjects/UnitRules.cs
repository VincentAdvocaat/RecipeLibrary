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
    /// Teaspoon and tablespoon allow discrete culinary fractions (¼, ⅓, ½, ⅔, ¾).
    /// </summary>
    public static bool AllowsCulinaryFractions(Unit unit) =>
        unit is Unit.Teaspoon or Unit.Tablespoon;

    /// <summary>
    /// Smallest culinary fractional step (¼). Thirds are also allowed via the fraction select.
    /// </summary>
    public static decimal InputStep(Unit unit) =>
        AllowsCulinaryFractions(unit) ? 0.25m : 1m;

    public static bool TryParse(string? value, out Unit unit)
    {
        var raw = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            unit = Unit.Unknown;
            return false;
        }

        if (!Enum.TryParse<Unit>(raw, ignoreCase: true, out unit) || unit == Unit.Unknown)
        {
            unit = Unit.Unknown;
            return false;
        }

        return true;
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
