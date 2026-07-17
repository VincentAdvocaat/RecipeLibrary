namespace RecipeLibrary.Application.Contracts;

/// <summary>
/// Optional AI-assisted parsing flags for recipe import queries.
/// </summary>
public sealed class ImportRecipeParseOptions
{
    /// <summary>
    /// When true, ingredient lines with confidence below the configured threshold are re-parsed via LLM.
    /// </summary>
    public bool UseAiFallback { get; init; } = true;

    /// <summary>
    /// When true, skip deterministic parsing and send the full normalized plain-text recipe to the LLM.
    /// </summary>
    public bool UseFullRecipeAi { get; init; }
}
