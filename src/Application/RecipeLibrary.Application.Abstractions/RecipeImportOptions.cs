namespace RecipeLibrary.Application.Abstractions;

public sealed class RecipeImportOptions
{
    public const string SectionName = "RecipeImport";

    public RecipeImportAiOptions Ai { get; init; } = new();

    public RecipeImportYouTubeOptions YouTube { get; init; } = new();

    public RecipeImportUrlFetchOptions UrlFetch { get; init; } = new();

    public RecipeImportOcrOptions Ocr { get; init; } = new();
}

public sealed class RecipeImportYouTubeOptions
{
    /// <summary>YouTube Data API v3 key. Empty or placeholder values skip the Data API path.</summary>
    public string? ApiKey { get; init; }
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

    public int MaxBytes { get; init; } = 2_097_152;
}

public sealed class RecipeImportOcrOptions
{
    /// <summary>Relative or absolute path to tessdata folder. Empty uses AppContext.BaseDirectory/tessdata.</summary>
    public string TessDataPath { get; init; } = string.Empty;

    public int MaxImageBytes { get; init; } = 8 * 1024 * 1024;

    /// <summary>Maximum number of photos/screenshots in one import request.</summary>
    public int MaxImagesPerImport { get; init; } = 5;
}
