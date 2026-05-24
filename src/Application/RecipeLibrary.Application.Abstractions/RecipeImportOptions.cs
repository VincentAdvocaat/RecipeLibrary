namespace RecipeLibrary.Application.Abstractions;

public sealed class RecipeImportOptions
{
    public const string SectionName = "RecipeImport";

    public RecipeImportAiOptions Ai { get; init; } = new();

    public RecipeImportUrlFetchOptions UrlFetch { get; init; } = new();
}

public sealed class RecipeImportAiOptions
{
    public bool Enabled { get; init; }

    public string Provider { get; init; } = "OpenAI";

    public string Model { get; init; } = "gpt-4o-mini";

    public string? ApiKey { get; init; }

    public string? Endpoint { get; init; }

    public decimal ConfidenceThreshold { get; init; } = 0.7m;
}

public sealed class RecipeImportUrlFetchOptions
{
    public int TimeoutSeconds { get; init; } = 10;

    public int MaxBytes { get; init; } = 524_288;
}
