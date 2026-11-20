using System.Globalization;
using System.Net;
using AngleSharp.Dom;
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

        var document = HtmlParser.ParseDocument(html);
        var recipe = FindBestRecipe(document);
        var body = document.QuerySelector("main article") ?? document.QuerySelector("article")
            ?? document.QuerySelector("main") ?? document.Body;
        var bodyText = body is null ? string.Empty : ReadVisibleLines(body);
        if (recipe is not { } structured)
        {
            return bodyText;
        }

        var structuredText = FormatRecipeNodeAsText(structured, bodyText);
        var draft = RecipeTextDocumentExtractor.Extract(structuredText);
        if (draft is { IngredientLines.Count: > 0, Steps.Count: > 0 }
            && !draft.Warnings.Contains(ImportWarningCodes.HeuristicIngredients))
        {
            return structuredText;
        }

        var visible = RecipeTextDocumentExtractor.Extract(bodyText);
        var reliableIngredients = visible.IngredientLines.Count > 0
            && !visible.Warnings.Contains(ImportWarningCodes.HeuristicIngredients);
        if (!string.Equals(draft.Title, visible.Title, StringComparison.OrdinalIgnoreCase))
        {
            return reliableIngredients && visible.Steps.Count > 0 ? bodyText : structuredText;
        }

        return FormatRecipeNodeAsText(structured, bodyText, visible, reliableIngredients);
    }

    private static JsonElement? FindBestRecipe(IDocument document)
    {
        JsonElement? best = null;
        var bestScore = 0;

        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            if (string.IsNullOrWhiteSpace(script.TextContent))
            {
                continue;
            }

            try
            {
                using var json = JsonDocument.Parse(script.TextContent);
                foreach (var candidate in FindRecipeNodes(json.RootElement))
                {
                    var ingredients = candidate.TryGetProperty("recipeIngredient", out var lines)
                        && ParseIngredientElements(lines).Any();
                    var steps = candidate.TryGetProperty("recipeInstructions", out var instructions)
                        && ParseInstructionTexts(instructions).Any();
                    var score = (ingredients ? 2 : 0) + (steps ? 1 : 0);
                    if (score > bestScore)
                    {
                        best = candidate.Clone();
                        bestScore = score;
                    }

                    if (bestScore == 3)
                    {
                        return best;
                    }
                }
            }
            catch (JsonException)
            {
                // A later script or the visible recipe may still be usable.
            }
        }

        return best;
    }

    private static string FormatRecipeNodeAsText(JsonElement recipe, string? bodyText,
        RecipeTextDocument? visible = null, bool reliableVisibleIngredients = false)
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

        // Keep prep/cook distinct so the text parser can populate both fields.
        if (prep is > 0)
        {
            sb.AppendLine($"Bereidingstijd: {prep.Value} M");
        }

        if (cook is > 0)
        {
            sb.AppendLine($"Kooktijd: {cook.Value} M");
        }

        var difficultyLabel = TryFindDifficultyLabel(bodyText);
        if (!string.IsNullOrWhiteSpace(difficultyLabel))
        {
            sb.AppendLine(difficultyLabel);
        }

        var servings = ParseServings(recipe);
        if (servings is > 0)
        {
            sb.AppendLine($"{servings.Value} porties");
        }

        sb.AppendLine();
        sb.Append("Ingrediënten:");
        if (!string.IsNullOrWhiteSpace(title))
        {
            sb.Append(' ').Append(title.Trim());
        }

        sb.AppendLine();
        sb.AppendLine();

        var ingredients = recipe.TryGetProperty("recipeIngredient", out var ingredientsElement)
            ? ParseIngredientElements(ingredientsElement).ToArray() : [];
        if (ingredients.Length == 0 && reliableVisibleIngredients && visible is not null)
        {
            ingredients = visible.IngredientLines.ToArray();
        }
        foreach (var line in ingredients)
        {
            sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.Append("Bereiding:");
        if (!string.IsNullOrWhiteSpace(title))
        {
            sb.Append(' ').Append(title.Trim());
        }

        sb.AppendLine();

        var instructions = recipe.TryGetProperty("recipeInstructions", out var instructionsElement)
            ? ParseInstructionTexts(instructionsElement).ToArray() : [];
        if (instructions.Length == 0 && visible is not null)
        {
            instructions = visible.Steps.Select(step => step.Text).ToArray();
        }
        foreach (var step in instructions)
        {
            sb.AppendLine(step);
        }

        return sb.ToString().Trim();
    }

    private static IEnumerable<JsonElement> FindRecipeNodes(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in element.EnumerateArray().SelectMany(FindRecipeNodes))
            {
                yield return candidate;
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("@type", out var types) && EnumerateStringValues(types).Any(IsRecipeType))
            {
                yield return element;
            }

            foreach (var candidate in element.EnumerateObject().SelectMany(property => FindRecipeNodes(property.Value)))
            {
                yield return candidate;
            }
        }
    }

    private static bool IsRecipeType(string value) =>
        value.Equals("Recipe", StringComparison.OrdinalIgnoreCase)
        || value.Equals("https://schema.org/Recipe", StringComparison.OrdinalIgnoreCase)
        || value.Equals("http://schema.org/Recipe", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> ParseIngredientElements(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return SplitRecipeLines(element.GetString());
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
            return SplitRecipeLines(element.GetString());
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().SelectMany(ParseInstructionTexts);
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            // HowToSection / ItemList: nest first so section titles are not treated as steps.
            if (element.TryGetProperty("itemListElement", out var nested))
            {
                return ParseInstructionTexts(nested);
            }

            return SplitRecipeLines(GetStringProperty(element, "text"));
        }

        return [];
    }

    private static int? ParseServings(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("recipeYield", out var yieldElement))
        {
            return null;
        }

        var candidates = EnumerateStringValues(yieldElement).Select(value => value.Trim()).ToArray();
        var labeled = candidates.FirstOrDefault(value => Regex.IsMatch(value,
            @"^(?:serves?|voor)\s+\d+$|^\d+\s+(?:servings?|personen|porties|portie)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        // Some recipe cards encode package yield as ["150", "150 ml"].
        // A bare number is a serving count only when the card supplies no other yield label.
        var count = labeled ?? (candidates.All(value => value.All(char.IsAsciiDigit))
            ? candidates.FirstOrDefault()
            : null);
        return count is not null && RecipeMetaLineParser.TryParseServingsPhrase(count, out var servings)
            ? servings
            : null;
    }

    private static IEnumerable<string> EnumerateStringValues(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }

            yield break;
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            yield return element.GetRawText();
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var value in EnumerateStringValues(item))
                {
                    yield return value;
                }
            }
        }
    }

    private static string? TryFindDifficultyLabel(string? bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return null;
        }

        foreach (var rawLine in bodyText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.Length > 160)
            {
                continue;
            }

            if (RecipeTextDocumentExtractor.TryParseDifficultyLabel(line, out var label))
            {
                return label;
            }
        }

        return null;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => WebUtility.HtmlDecode(value.GetString()),
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

        int.TryParse(match.Groups["hours"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours);
        int.TryParse(match.Groups["minutes"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes);
        return hours <= (int.MaxValue - minutes) / 60 && hours * 60 + minutes > 0
            ? hours * 60 + minutes
            : null;
    }

    private static IEnumerable<string> SplitRecipeLines(string? value)
    {
        var text = WebUtility.HtmlDecode(value ?? string.Empty);
        if (text.Contains('<', StringComparison.Ordinal) && text.Contains('>', StringComparison.Ordinal))
        {
            var fragment = HtmlParser.ParseDocument(text).Body;
            text = fragment is null ? text : ReadVisibleLines(fragment);
        }

        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string ReadVisibleLines(INode root)
    {
        var text = new StringBuilder();
        AppendVisibleText(root, text, 0);
        return Regex.Replace(text.ToString(), @"[ \t]+\n", "\n").Trim();
    }

    private static void AppendVisibleText(INode node, StringBuilder text, int depth)
    {
        if (depth > 128)
        {
            return;
        }

        if (node is IText content)
        {
            text.Append(content.Data);
            return;
        }

        if (node is IElement { LocalName: "script" or "style" or "noscript" or "nav" or "footer" or "aside" or "template" }
            || node is IElement element && element.HasAttribute("hidden"))
        {
            return;
        }

        var block = node is IElement { LocalName: "p" or "div" or "li" or "br" or "h1" or "h2" or "h3" or "h4" or "section" };
        if (block)
        {
            text.AppendLine();
        }
        foreach (var child in node.ChildNodes)
        {
            AppendVisibleText(child, text, depth + 1);
        }
        if (block)
        {
            text.AppendLine();
        }
    }
}
