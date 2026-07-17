using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Parses normalized recipe plain text into a full import draft via LLM.
/// </summary>
public interface IRecipeAiParser
{
    Task<ImportRecipeResult> ParseAsync(string plainText, CancellationToken ct = default);
}
