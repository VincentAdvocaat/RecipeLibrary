using System.Globalization;
using Microsoft.AspNetCore.Http;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Web.Services;

/// <summary>
/// Reads/writes the measure-system preference cookie (orthogonal to UI culture).
/// </summary>
public sealed class MeasureSystemService(IHttpContextAccessor httpContextAccessor)
{
    public const string CookieName = "RecipeLibrary.MeasureSystem";

    public MeasureSystem GetMeasureSystem()
    {
        var context = httpContextAccessor.HttpContext;
        if (context?.Request.Cookies.TryGetValue(CookieName, out var value) == true
            && TryParse(value, out var parsed))
        {
            return parsed;
        }

        return MeasureSystemDefaults.ForCulture(CultureInfo.CurrentUICulture);
    }

    public static bool TryParse(string? value, out MeasureSystem measureSystem)
    {
        if (string.Equals(value, nameof(MeasureSystem.Metric), StringComparison.OrdinalIgnoreCase))
        {
            measureSystem = MeasureSystem.Metric;
            return true;
        }

        if (string.Equals(value, nameof(MeasureSystem.Imperial), StringComparison.OrdinalIgnoreCase))
        {
            measureSystem = MeasureSystem.Imperial;
            return true;
        }

        measureSystem = default;
        return false;
    }

    public static CookieOptions CreateCookieOptions() =>
        new()
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            HttpOnly = true,
        };
}
