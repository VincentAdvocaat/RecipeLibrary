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

        // US customary default; en-GB and other cultures use metric for cooking.
        if (culture.Name.Equals("en-US", StringComparison.OrdinalIgnoreCase))
        {
            return MeasureSystem.Imperial;
        }

        return MeasureSystem.Metric;
    }
}
