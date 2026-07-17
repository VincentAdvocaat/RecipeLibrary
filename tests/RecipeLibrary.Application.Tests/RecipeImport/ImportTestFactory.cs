using System.Text.Json;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.RecipeImport;

namespace RecipeLibrary.Application.Tests.RecipeImport;

internal static class ImportTestFactory
{
    internal static IOptions<RecipeImportOptions> AiEnabledOptions { get; } = Options.Create(new RecipeImportOptions
    {
        Ai = new RecipeImportAiOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            ConfidenceThreshold = 0.7m,
        },
    });

    internal static IOptions<RecipeImportOptions> AiDisabledOptions { get; } = Options.Create(new RecipeImportOptions());

    internal static RecipeTextParser CreateTextParser(
        IIngredientLineAiParser? aiParser = null,
        IOptions<RecipeImportOptions>? options = null) =>
        new(
            new IngredientLineParser(new IngredientLineResolver(new IngredientNameParser())),
            aiParser ?? new TestNullIngredientLineAiParser(),
            options ?? AiDisabledOptions);

    internal static RecipeImportService CreateImportService(
        IIngredientLineAiParser? aiParser = null,
        IRecipeAiParser? recipeAiParser = null,
        IOptions<RecipeImportOptions>? options = null) =>
        new(
            CreateTextParser(aiParser, options),
            new HtmlRecipeTextExtractor(),
            new IngredientMatcher(
                new EmptyIngredientRepository(),
                new IngredientTextNormalizer(),
                new IngredientSimilarityScorer()),
            recipeAiParser ?? new TestNullRecipeAiParser(),
            options ?? AiDisabledOptions);

    internal sealed class TestNullIngredientLineAiParser : IIngredientLineAiParser
    {
        public Task<IReadOnlyList<AiParsedIngredientLine>> ParseLinesAsync(
            IReadOnlyList<string> rawLines,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AiParsedIngredientLine>>([]);
    }

    internal sealed class TestNullRecipeAiParser : IRecipeAiParser
    {
        public Task<ImportRecipeResult> ParseAsync(string plainText, CancellationToken ct = default) =>
            throw new InvalidOperationException("Full-recipe AI parsing is not configured.");
    }

    internal sealed class EmptyIngredientRepository : IngredientRepositoryStub;
}

internal sealed class FixtureAiIngredientLineParser(string fixtureRelativePath) : IIngredientLineAiParser
{
    private readonly Dictionary<string, AiParsedIngredientLine> _overrides = LoadOverrides(fixtureRelativePath);

    public Task<IReadOnlyList<AiParsedIngredientLine>> ParseLinesAsync(
        IReadOnlyList<string> rawLines,
        CancellationToken ct = default)
    {
        var results = rawLines
            .Select(line =>
            {
                if (!_overrides.TryGetValue(line, out var mapped))
                {
                    throw new InvalidOperationException($"No AI fixture override for ingredient line: {line}");
                }

                return new AiParsedIngredientLine
                {
                    RawLine = line,
                    Quantity = mapped.Quantity,
                    Unit = mapped.Unit,
                    Name = mapped.Name,
                    Preparation = mapped.Preparation,
                    Confidence = mapped.Confidence,
                };
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<AiParsedIngredientLine>>(results);
    }

    private static Dictionary<string, AiParsedIngredientLine> LoadOverrides(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
        var json = File.ReadAllText(path);
        var items = JsonSerializer.Deserialize<Dictionary<string, AiIngredientOverride>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to load AI overrides from {path}.");

        return items.ToDictionary(
            pair => pair.Key,
            pair => new AiParsedIngredientLine
            {
                RawLine = pair.Key,
                Quantity = pair.Value.Quantity,
                Unit = pair.Value.Unit,
                Name = pair.Value.Name ?? string.Empty,
                Preparation = pair.Value.Preparation,
                Confidence = pair.Value.Confidence ?? 0.9m,
            },
            StringComparer.Ordinal);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class AiIngredientOverride
    {
        public decimal? Quantity { get; set; }

        public string? Unit { get; set; }

        public string? Name { get; set; }

        public string? Preparation { get; set; }

        public decimal? Confidence { get; set; }
    }
}
