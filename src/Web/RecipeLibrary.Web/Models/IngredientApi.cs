using System.Globalization;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Web.Models;

/// <summary>
/// Builds ingredient match commands with the Blazor circuit UI culture.
/// </summary>
public static class IngredientApi
{
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
