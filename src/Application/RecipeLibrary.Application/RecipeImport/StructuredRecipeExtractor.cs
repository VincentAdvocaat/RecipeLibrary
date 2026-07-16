using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.RecipeImport;

public sealed class StructuredRecipeExtractor
{
    private static readonly HtmlParser HtmlParser = new();
    public StructuredRecipeExtraction Extract(string content, ImportContentKind contentKind)
    {
        var warnings = new List<string>();
        var isHtml = contentKind switch
        {
            ImportContentKind.Html => true,
            ImportContentKind.PlainText => false,
            _ => LooksLikeHtml(content),
        };

        if (isHtml)
        {
            var jsonLd = TryExtractJsonLd(content, warnings);
            if (jsonLd is not null)
            {
                return jsonLd;
            }
        }

        var plainText = isHtml ? HtmlToPlainText(content) : content;
        return PlainTextSectionExtractor.Extract(plainText, warnings);
    }

    private static bool LooksLikeHtml(string content) =>
        content.Contains('<', StringComparison.Ordinal) && content.Contains('>', StringComparison.Ordinal);

    private static StructuredRecipeExtraction? TryExtractJsonLd(string html, List<string> warnings)
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

                return MapRecipeNode(recipe.Value, warnings);
            }
            catch (JsonException)
            {
                warnings.Add(ImportWarningCodes.JsonLdParseSkipped);
            }
        }

        return null;
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

    private static StructuredRecipeExtraction MapRecipeNode(JsonElement recipe, List<string> warnings)
    {
        var title = GetStringProperty(recipe, "name");
        var description = GetStringProperty(recipe, "description");
        var prep = ParseIsoDurationMinutes(GetStringProperty(recipe, "prepTime"));
        var cook = ParseIsoDurationMinutes(GetStringProperty(recipe, "cookTime"));

        var ingredientLines = new List<string>();
        if (recipe.TryGetProperty("recipeIngredient", out var ingredientsElement))
        {
            ingredientLines.AddRange(ParseIngredientElements(ingredientsElement));
        }

        var steps = new List<ImportedInstructionStep>();
        if (recipe.TryGetProperty("recipeInstructions", out var instructionsElement))
        {
            steps.AddRange(ParseInstructionElements(instructionsElement));
        }

        if (ingredientLines.Count == 0)
        {
            warnings.Add(ImportWarningCodes.JsonLdEmptyIngredients);
        }

        return new StructuredRecipeExtraction
        {
            Title = title,
            Description = description,
            PreparationTimeMinutes = prep,
            CookingTimeMinutes = cook,
            IngredientLines = ingredientLines,
            Steps = steps,
            Source = ImportSource.JsonLd,
            Warnings = warnings,
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

    private static IEnumerable<ImportedInstructionStep> ParseInstructionElements(JsonElement element)
    {
        var steps = new List<ImportedInstructionStep>();

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                steps.Add(new ImportedInstructionStep { StepNumber = 1, Text = text });
            }

            return steps;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var stepNumber = 1;
            foreach (var item in element.EnumerateArray())
            {
                foreach (var parsed in ParseInstructionElements(item))
                {
                    steps.Add(new ImportedInstructionStep
                    {
                        StepNumber = stepNumber++,
                        Text = parsed.Text,
                    });
                }
            }

            return steps;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var text = GetStringProperty(element, "text")
                ?? GetStringProperty(element, "name")
                ?? GetStringProperty(element, "itemListElement");

            if (!string.IsNullOrWhiteSpace(text))
            {
                steps.Add(new ImportedInstructionStep { StepNumber = 1, Text = text.Trim() });
            }
        }

        return steps;
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

    private static string HtmlToPlainText(string html)
    {
        var document = HtmlParser.ParseDocument(html);
        return document.Body?.TextContent ?? html;
    }
}

public sealed class PlainTextSectionExtractor
{
    private static readonly string[] IngredientSectionHeaders =
    [
        "ingrediënten",
        "ingredienten",
        "benodigdheden",
        "ingredients",
    ];

    private static readonly string[] InstructionSectionHeaders =
    [
        "bereiding",
        "werkwijze",
        "instructies",
        "stappen",
        "instructions",
    ];

    public static StructuredRecipeExtraction Extract(string plainText, List<string> warnings)
    {
        var lines = plainText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        if (lines.Count == 0)
        {
            warnings.Add(ImportWarningCodes.NoContent);
            return new StructuredRecipeExtraction
            {
                Source = ImportSource.PlainText,
                Warnings = warnings,
            };
        }

        var title = lines[0];
        var descriptionLines = new List<string>();
        var ingredientLines = new List<string>();
        var instructionLines = new List<string>();

        var section = Section.None;
        for (var i = 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (TryGetSection(line, IngredientSectionHeaders, out _))
            {
                section = Section.Ingredients;
                continue;
            }

            if (TryGetSection(line, InstructionSectionHeaders, out _))
            {
                section = Section.Instructions;
                continue;
            }

            switch (section)
            {
                case Section.Ingredients:
                    ingredientLines.Add(StripBullet(line));
                    break;
                case Section.Instructions:
                    instructionLines.Add(StripNumberPrefix(line));
                    break;
                default:
                    descriptionLines.Add(line);
                    break;
            }
        }

        if (ingredientLines.Count == 0)
        {
            warnings.Add(ImportWarningCodes.HeuristicIngredients);
            ingredientLines.AddRange(lines.Skip(1).Where(LooksLikeIngredientLine).Select(StripBullet));
        }

        var steps = instructionLines
            .Select((text, index) => new ImportedInstructionStep { StepNumber = index + 1, Text = text })
            .ToList();

        return new StructuredRecipeExtraction
        {
            Title = title,
            Description = descriptionLines.Count > 0 ? string.Join("\n", descriptionLines) : null,
            IngredientLines = ingredientLines,
            Steps = steps,
            Source = ImportSource.PlainText,
            Warnings = warnings,
        };
    }

    private enum Section
    {
        None,
        Ingredients,
        Instructions,
    }

    private static bool TryGetSection(string line, string[] headers, out string header)
    {
        header = string.Empty;
        var normalized = line.Trim().TrimEnd(':').ToLowerInvariant();
        foreach (var candidate in headers)
        {
            if (normalized.Equals(candidate, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(candidate + ":", StringComparison.OrdinalIgnoreCase))
            {
                header = candidate;
                return true;
            }
        }

        return false;
    }

    private static string StripBullet(string line) =>
        line.TrimStart('-', '•', '*', '▢', ' ').Trim();

    private static string StripNumberPrefix(string line) =>
        Regex.Replace(line, @"^\d+[\.)]\s*", string.Empty).Trim();

    private static bool LooksLikeIngredientLine(string line)
    {
        var trimmed = StripBullet(line);
        return Regex.IsMatch(trimmed, @"^(\d+(?:[.,]\d+)?|\d+\s*/\s*\d+|\d+\s*-\s*\d+|snuf|snufje|handje)\b", RegexOptions.IgnoreCase)
            || trimmed.Contains(" naar smaak", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class StructuredRecipeExtraction
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public int? PreparationTimeMinutes { get; init; }

    public int? CookingTimeMinutes { get; init; }

    public IReadOnlyList<string> IngredientLines { get; init; } = [];

    public IReadOnlyList<ImportedInstructionStep> Steps { get; init; } = [];

    public ImportSource Source { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];
}
