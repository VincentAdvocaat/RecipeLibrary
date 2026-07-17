namespace RecipeLibrary.Application.Abstractions;

public interface IIngredientLineAiParser
{
    Task<IReadOnlyList<AiParsedIngredientLine>> ParseLinesAsync(
        IReadOnlyList<string> rawLines,
        CancellationToken ct = default);
}

public sealed class AiParsedIngredientLine
{
    public string RawLine { get; init; } = string.Empty;

    public decimal? Quantity { get; init; }

    public string? Unit { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Preparation { get; init; }

    public decimal Confidence { get; init; }
}

public interface IRecipeImportContentFetcher
{
    Task<string> FetchHtmlAsync(string url, CancellationToken ct = default);
}

public interface IRecipeImageTextExtractor
{
    Task<string> ExtractTextAsync(Stream imageStream, string language, CancellationToken ct = default);
}
