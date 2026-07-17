using System.Globalization;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Web.Models;

/// <summary>
/// Builds ingredient API requests with the Blazor circuit UI culture.
/// Loopback <see cref="HttpClient"/> calls do not send the culture cookie, so culture must be explicit
/// (and/or Accept-Language on the client).
/// </summary>
public static class IngredientApi
{
    public static string SearchUrl(string query, string? cultureName = null)
    {
        var culture = ResolveCulture(cultureName);
        return $"/ingredients/search?q={Uri.EscapeDataString(query)}&culture={Uri.EscapeDataString(culture)}";
    }

    public static MatchIngredientCommand MatchCommand(string input, string? cultureName = null) =>
        new()
        {
            Input = input,
            CultureName = ResolveCulture(cultureName),
        };

    private static string ResolveCulture(string? cultureName) =>
        string.IsNullOrWhiteSpace(cultureName)
            ? CultureInfo.CurrentUICulture.Name
            : cultureName.Trim();
}
