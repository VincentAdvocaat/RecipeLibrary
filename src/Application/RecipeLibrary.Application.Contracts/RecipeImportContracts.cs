namespace RecipeLibrary.Application.Contracts;

public enum ImportContentKind
{
    Auto = 0,
    Html = 1,
    PlainText = 2,
}

public enum ImportSource
{
    Unknown = 0,
    JsonLd = 1,
    PlainText = 2,
    Mixed = 3,
}

public enum ImportParseMethod
{
    Structured = 0,
    Deterministic = 1,
    Ai = 2,
}

public sealed class ImportRecipeContentQuery : IQuery<ImportRecipeResult>
{
    public string Content { get; init; } = string.Empty;

    public ImportContentKind ContentKind { get; init; } = ImportContentKind.Auto;
}

public sealed class ImportRecipeFromUrlQuery : IQuery<ImportRecipeResult>
{
    public string Url { get; init; } = string.Empty;
}

public sealed class ImportRecipeResult
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public int? PreparationTimeMinutes { get; init; }

    public int? CookingTimeMinutes { get; init; }

    public ImportSource Source { get; init; }

    public IReadOnlyList<ImportedIngredientLine> Ingredients { get; init; } = [];

    public IReadOnlyList<ImportedInstructionStep> Steps { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class ImportedIngredientLine
{
    public string RawLine { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public string Unit { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Preparation { get; init; }

    public decimal Confidence { get; init; }

    public ImportParseMethod ParseMethod { get; init; }

    public string? MatchType { get; init; }
}

public sealed class ImportedInstructionStep
{
    public int StepNumber { get; set; }

    public string Text { get; set; } = string.Empty;
}
