using System.Globalization;
using System.Text.RegularExpressions;

namespace RecipeLibrary.Application.ShoppingLists;

public static class ShoppingListDefaultNameBuilder
{
    public static string GetNextNumberedName(string nameFormat, IEnumerable<string> existingNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameFormat);

        var placeholderIndex = nameFormat.IndexOf("{0}", StringComparison.Ordinal);
        if (placeholderIndex < 0)
        {
            return string.Format(CultureInfo.CurrentCulture, nameFormat, 1);
        }

        var prefix = nameFormat[..placeholderIndex].TrimEnd();
        var suffix = nameFormat[(placeholderIndex + 3)..];
        var pattern = $"^{Regex.Escape(prefix)}\\s*(\\d+){Regex.Escape(suffix)}$";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var usedNumbers = new HashSet<int>();
        foreach (var name in existingNames)
        {
            var match = regex.Match(name.Trim());
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                usedNumbers.Add(number);
            }
        }

        var next = 1;
        while (usedNumbers.Contains(next))
        {
            next++;
        }

        return string.Format(CultureInfo.CurrentCulture, nameFormat, next);
    }
}
