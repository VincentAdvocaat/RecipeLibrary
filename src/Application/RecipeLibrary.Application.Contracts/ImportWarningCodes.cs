namespace RecipeLibrary.Application.Contracts;

/// <summary>Stable warning codes emitted by recipe import; UI maps these to localized strings.</summary>
public static class ImportWarningCodes
{
    public const string JsonLdParseSkipped = "jsonld_parse_skipped";
    public const string JsonLdEmptyIngredients = "jsonld_empty_ingredients";
    public const string NoContent = "no_content";
    public const string HeuristicIngredients = "heuristic_ingredients";
    public const string LowConfidenceAiHint = "low_confidence_ai_hint";
    public const string AiFallbackFailed = "ai_fallback_failed";
    public const string UrlContentTruncated = "url_content_truncated";
}
