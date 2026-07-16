using System.Globalization;

namespace RecipeLibrary.Application.Ingredients;

/// <summary>
/// Discrete culinary fraction parts for teaspoon/tablespoon quantities.
/// </summary>
public static class CulinaryQuantityFractions
{
    public const decimal Quarter = 0.25m;
    public const decimal Third = 1m / 3m;
    public const decimal Half = 0.5m;
    public const decimal TwoThirds = 2m / 3m;
    public const decimal ThreeQuarters = 0.75m;

    /// <summary>
    /// Allowed fractional parts (including 0 for whole numbers only).
    /// </summary>
    public static IReadOnlyList<decimal> FractionalParts { get; } =
    [
        0m,
        Quarter,
        Third,
        Half,
        TwoThirds,
        ThreeQuarters
    ];

    /// <summary>
    /// Options for the fraction select: value key and display label.
    /// </summary>
    public static IReadOnlyList<(string Key, string Label, decimal Value)> SelectOptions { get; } =
    [
        ("0", "—", 0m),
        ("1/4", "¼", Quarter),
        ("1/3", "⅓", Third),
        ("1/2", "½", Half),
        ("2/3", "⅔", TwoThirds),
        ("3/4", "¾", ThreeQuarters)
    ];

    private const decimal SnapTolerance = 0.02m;

    public static decimal Combine(int whole, decimal fraction)
    {
        if (whole < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(whole), "Whole part cannot be negative.");
        }

        if (!IsAllowedFractionalPart(fraction))
        {
            throw new ArgumentOutOfRangeException(nameof(fraction), "Fractional part is not a culinary fraction.");
        }

        return whole + fraction;
    }

    public static (int Whole, decimal Fraction) Split(decimal quantity)
    {
        if (!TrySnap(quantity, out var snapped))
        {
            snapped = SnapToNearest(quantity);
        }

        var whole = (int)decimal.Floor(snapped);
        var fraction = snapped - whole;
        if (!IsAllowedFractionalPart(fraction))
        {
            fraction = FindClosestFractionalPart(fraction);
            snapped = whole + fraction;
            whole = (int)decimal.Floor(snapped);
            fraction = snapped - whole;
        }

        return (whole, fraction);
    }

    public static bool TrySnap(decimal quantity, out decimal snapped)
    {
        snapped = 0m;
        if (quantity <= 0m)
        {
            return false;
        }

        var candidate = SnapToNearest(quantity);
        if (candidate <= 0m)
        {
            return false;
        }

        if (Math.Abs(quantity - candidate) > SnapTolerance)
        {
            return false;
        }

        snapped = candidate;
        return true;
    }

    public static decimal SnapToNearest(decimal quantity)
    {
        if (quantity <= 0m)
        {
            return 0m;
        }

        var whole = decimal.Floor(quantity);
        var frac = quantity - whole;

        var bestFrac = FindClosestFractionalPart(frac);
        var distToFrac = Math.Abs(frac - bestFrac);
        var distToNextWhole = Math.Abs(frac - 1m);

        if (distToNextWhole < distToFrac)
        {
            return whole + 1m;
        }

        var snapped = whole + bestFrac;
        return snapped > 0m ? snapped : bestFrac > 0m ? bestFrac : 0m;
    }

    public static string FormatMixed(decimal quantity)
    {
        if (!TrySnap(quantity, out var snapped))
        {
            snapped = SnapToNearest(quantity);
        }

        if (snapped <= 0m)
        {
            return "0";
        }

        var whole = (int)decimal.Floor(snapped);
        var fraction = snapped - whole;
        var fractionLabel = ToUnicode(fraction);

        if (fractionLabel is null)
        {
            return whole.ToString(CultureInfo.InvariantCulture);
        }

        if (whole == 0)
        {
            return fractionLabel;
        }

        return string.Concat(whole.ToString(CultureInfo.InvariantCulture), fractionLabel);
    }

    public static string FractionKey(decimal fraction)
    {
        foreach (var option in SelectOptions)
        {
            if (option.Value == fraction)
            {
                return option.Key;
            }
        }

        var closest = FindClosestFractionalPart(fraction);
        foreach (var option in SelectOptions)
        {
            if (option.Value == closest)
            {
                return option.Key;
            }
        }

        return "0";
    }

    public static bool TryParseFractionKey(string? key, out decimal fraction)
    {
        foreach (var option in SelectOptions)
        {
            if (string.Equals(option.Key, key, StringComparison.Ordinal))
            {
                fraction = option.Value;
                return true;
            }
        }

        fraction = 0m;
        return false;
    }

    private static bool IsAllowedFractionalPart(decimal fraction) =>
        FractionalParts.Any(part => part == fraction);

    private static decimal FindClosestFractionalPart(decimal fraction)
    {
        var best = 0m;
        var bestDist = decimal.MaxValue;
        foreach (var part in FractionalParts)
        {
            var dist = Math.Abs(fraction - part);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = part;
            }
        }

        return best;
    }

    private static string? ToUnicode(decimal fraction) => fraction switch
    {
        Quarter => "¼",
        Third => "⅓",
        Half => "½",
        TwoThirds => "⅔",
        ThreeQuarters => "¾",
        0m => null,
        _ => null
    };
}
