using System.Globalization;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Ingredients;

/// <summary>
/// Culture → default <see cref="MeasureSystem"/> mapping (orthogonal to UI language once a cookie is set).
/// </summary>
public static class MeasureSystemDefaults
{
    public static MeasureSystem ForCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var name = culture.Name;
        if (name.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            || name.Equals("en-GB", StringComparison.OrdinalIgnoreCase))
        {
            return MeasureSystem.Imperial;
        }

        return MeasureSystem.Metric;
    }
}
