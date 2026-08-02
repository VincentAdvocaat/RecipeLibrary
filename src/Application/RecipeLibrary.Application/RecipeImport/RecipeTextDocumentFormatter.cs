using System.Text;

namespace RecipeLibrary.Application.RecipeImport;

internal static class RecipeTextDocumentFormatter
{
    internal static string NormalizePlainTextForAi(string plainText)
    {
        var document = RecipeTextDocumentExtractor.Extract(plainText ?? string.Empty);
        var normalized = FormatNormalizedPlainText(document);
        return string.IsNullOrWhiteSpace(normalized) ? plainText ?? string.Empty : normalized;
    }

    internal static string FormatNormalizedPlainText(RecipeTextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(document.Title)) sb.AppendLine(document.Title.Trim());
        if (!string.IsNullOrWhiteSpace(document.Description))
        {
            EnsureBlankLine(sb);
            sb.AppendLine(document.Description.Trim());
        }
        if (document.PreparationTimeMinutes is not null || document.CookingTimeMinutes is not null || document.Servings is not null)
        {
            EnsureBlankLine(sb);
            if (document.PreparationTimeMinutes is int prep) sb.AppendLine($"Prep time: {prep} min");
            if (document.CookingTimeMinutes is int cook) sb.AppendLine($"Cook time: {cook} min");
            if (document.Servings is int servings) sb.AppendLine($"Servings: {servings}");
        }
        if (document.IngredientLines.Count > 0)
        {
            EnsureBlankLine(sb);
            sb.AppendLine("Ingredients");
            foreach (var line in document.IngredientLines.Where(static line => !string.IsNullOrWhiteSpace(line))) sb.AppendLine(line.Trim());
        }
        if (document.Steps.Count > 0)
        {
            EnsureBlankLine(sb);
            sb.AppendLine("Instructions");
            foreach (var step in document.Steps.Where(static step => !string.IsNullOrWhiteSpace(step.Text)))
            {
                sb.AppendLine(step.StepNumber > 0 ? $"{step.StepNumber}. {step.Text.Trim()}" : step.Text.Trim());
            }
        }
        return sb.ToString().Trim();
    }

    private static void EnsureBlankLine(StringBuilder sb)
    {
        if (sb.Length > 0) sb.AppendLine();
    }
}
