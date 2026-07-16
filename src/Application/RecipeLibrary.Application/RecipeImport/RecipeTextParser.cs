using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;

namespace RecipeLibrary.Application.RecipeImport;

/// <summary>
/// Parses normalized recipe plain text into an import draft (shared by paste/URL/image).
/// </summary>
public sealed class RecipeTextParser(IngredientLineParser ingredientLineParser)
{
    public ImportRecipeResult Parse(string plainText)
    {
        var document = RecipeTextDocumentExtractor.Extract(plainText ?? string.Empty);
        var ingredients = new List<ImportedIngredientLine>();

        foreach (var rawLine in document.IngredientLines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var parsed = ingredientLineParser.Parse(rawLine);
            ingredients.Add(new ImportedIngredientLine
            {
                RawLine = parsed.RawLine,
                Quantity = parsed.Quantity,
                Unit = parsed.Unit,
                Name = parsed.Name,
                Preparation = parsed.Preparation,
                Confidence = parsed.Confidence,
                ParseMethod = parsed.ParseMethod,
            });
        }

        return new ImportRecipeResult
        {
            Title = document.Title,
            Description = document.Description,
            PreparationTimeMinutes = document.PreparationTimeMinutes,
            CookingTimeMinutes = document.CookingTimeMinutes,
            Difficulty = document.Difficulty,
            Category = document.Category,
            Servings = document.Servings,
            Source = ImportSource.PlainText,
            Ingredients = ingredients,
            Steps = document.Steps,
            Warnings = document.Warnings,
        };
    }
}
