using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.RecipeImport;

/// <summary>
/// Converts HTML (including JSON-LD Recipe) into normalized recipe plain text for <see cref="RecipeTextParser"/>.
/// </summary>
public sealed class HtmlRecipeTextExtractor
{
    private static readonly HtmlParser HtmlParser = new();

    public string Extract(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var warnings = new List<string>();
        var fromJsonLd = TryFormatJsonLdAsText(html, warnings);
        if (!string.IsNullOrWhiteSpace(fromJsonLd))
        {
            return fromJsonLd;
        }

        var document = HtmlParser.ParseDocument(html);
        var bodyText = document.Body?.TextContent ?? html;
        return NormalizeWhitespace(bodyText);
    }

    private static string? TryFormatJsonLdAsText(string html, List<string> warnings)
    {
        var document = HtmlParser.ParseDocument(html);
        var scripts = document.QuerySelectorAll("script[type='application/ld+json']");

        foreach (var script in scripts)
        {
            var json = script.TextContent;
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var recipe = FindRecipeNode(doc.RootElement);
                if (recipe is null)
                {
                    continue;
                }

                return FormatRecipeNodeAsText(recipe.Value);
            }
            catch (JsonException)
            {
                warnings.Add(ImportWarningCodes.JsonLdParseSkipped);
            }
        }

        return null;
    }

    private static string FormatRecipeNodeAsText(JsonElement recipe)
    {
        var sb = new StringBuilder();
        var title = GetStringProperty(recipe, "name");
        var description = GetStringProperty(recipe, "description");
        var prep = ParseIsoDurationMinutes(GetStringProperty(recipe, "prepTime"));
        var cook = ParseIsoDurationMinutes(GetStringProperty(recipe, "cookTime"));

        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine("Inleiding:");
            sb.AppendLine(description.Trim());
            sb.AppendLine();
        }

        var totalMinutes = (prep ?? 0) + (cook ?? 0);
        if (totalMinutes > 0)
        {
            sb.AppendLine($"{totalMinutes} M");
        }
        else if (cook is > 0)
        {
            sb.AppendLine($"{cook.Value} M");
        }
        else if (prep is > 0)
        {
            sb.AppendLine($"{prep.Value} M");
        }

        sb.AppendLine();
        sb.Append("Ingrediënten:");
        if (!string.IsNullOrWhiteSpace(title))
        {
            sb.Append(' ').Append(title.Trim());
        }

        sb.AppendLine();
        sb.AppendLine();

        if (recipe.TryGetProperty("recipeIngredient", out var ingredientsElement))
        {
            foreach (var line in ParseIngredientElements(ingredientsElement))
            {
                sb.AppendLine(line);
            }
        }

        sb.AppendLine();
        sb.Append("Bereiding:");
        if (!string.IsNullOrWhiteSpace(title))
        {
            sb.Append(' ').Append(title.Trim());
        }

        sb.AppendLine();

        if (recipe.TryGetProperty("recipeInstructions", out var instructionsElement))
        {
            foreach (var step in ParseInstructionTexts(instructionsElement))
            {
                sb.AppendLine(step);
            }
        }

        return sb.ToString().Trim();
    }

    private static JsonElement? FindRecipeNode(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = FindRecipeNode(item);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (IsRecipeType(element))
        {
            return element;
        }

        if (element.TryGetProperty("@graph", out var graph))
        {
            return FindRecipeNode(graph);
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                var found = FindRecipeNode(property.Value);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static bool IsRecipeType(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var typeElement))
        {
            return false;
        }

        return typeElement.ValueKind switch
        {
            JsonValueKind.String => typeElement.GetString()?.Contains("Recipe", StringComparison.OrdinalIgnoreCase) == true,
            JsonValueKind.Array => typeElement.EnumerateArray().Any(x =>
                x.GetString()?.Contains("Recipe", StringComparison.OrdinalIgnoreCase) == true),
            _ => false,
        };
    }

    private static IEnumerable<string> ParseIngredientElements(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            return string.IsNullOrWhiteSpace(value) ? [] : [value.Trim()];
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().SelectMany(ParseIngredientElements);
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var amount = GetStringProperty(element, "amount");
            var name = GetStringProperty(element, "name");
            if (!string.IsNullOrWhiteSpace(amount) && !string.IsNullOrWhiteSpace(name))
            {
                return [$"{amount.Trim()} {name.Trim()}"];
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return [name.Trim()];
            }
        }

        return [];
    }

    private static IEnumerable<string> ParseInstructionTexts(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(text) ? [] : [text];
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().SelectMany(ParseInstructionTexts);
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var text = GetStringProperty(element, "text")
                ?? GetStringProperty(element, "name");
            if (!string.IsNullOrWhiteSpace(text))
            {
                return [text.Trim()];
            }
        }

        return [];
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static int? ParseIsoDurationMinutes(string? isoDuration)
    {
        if (string.IsNullOrWhiteSpace(isoDuration))
        {
            return null;
        }

        var match = Regex.Match(
            isoDuration,
            @"PT(?:(?<hours>\d+)H)?(?:(?<minutes>\d+)M)?",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return null;
        }

        var hours = match.Groups["hours"].Success
            ? int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture)
            : 0;
        var minutes = match.Groups["minutes"].Success
            ? int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture)
            : 0;

        var total = hours * 60 + minutes;
        return total > 0 ? total : null;
    }

    private static string NormalizeWhitespace(string text) =>
        Regex.Replace(text.Replace("\r\n", "\n", StringComparison.Ordinal), @"[ \t]+\n", "\n").Trim();
}
