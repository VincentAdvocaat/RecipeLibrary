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

public sealed class ImportRecipeFromImageQuery : IQuery<ImportRecipeResult>
{
    public byte[] ImageBytes { get; init; } = [];

    public string ContentType { get; init; } = string.Empty;

    /// <summary>Original file name; used to infer content type when the client sends a generic type.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Tesseract language code: nld or eng.</summary>
    public string Language { get; init; } = "nld";
}

public sealed class CreateRecipeImportImageSessionCommand : ICommand<CreateRecipeImportImageSessionResult>
{
    public string OwnerKey { get; init; } = string.Empty;
}

public sealed class CreateRecipeImportImageSessionResult
{
    public string SessionId { get; init; } = string.Empty;

    public DateTimeOffset ExpiresUtc { get; init; }
}

public sealed class AddRecipeImportImageCommand : ICommand<AddRecipeImportImageResult>
{
    public string SessionId { get; init; } = string.Empty;

    public string OwnerKey { get; init; } = string.Empty;

    public byte[] ImageBytes { get; init; } = [];

    public string ContentType { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;
}

public sealed class AddRecipeImportImageResult
{
    public string ImageId { get; init; } = string.Empty;

    public int Order { get; init; }

    public string FileName { get; init; } = string.Empty;

    public int ImageCount { get; init; }
}

public sealed class RemoveRecipeImportImageCommand : ICommand<RemoveRecipeImportImageResult>
{
    public string SessionId { get; init; } = string.Empty;

    public string OwnerKey { get; init; } = string.Empty;

    public string ImageId { get; init; } = string.Empty;
}

public sealed class RemoveRecipeImportImageResult
{
    public int ImageCount { get; init; }
}

public sealed class DeleteRecipeImportImageSessionCommand : ICommand<DeleteRecipeImportImageSessionResult>
{
    public string SessionId { get; init; } = string.Empty;

    public string OwnerKey { get; init; } = string.Empty;
}

public sealed class DeleteRecipeImportImageSessionResult
{
    public bool Deleted { get; init; }
}

public sealed class ProcessRecipeImportImageSessionQuery : IQuery<ImportRecipeResult>
{
    public string SessionId { get; init; } = string.Empty;

    public string OwnerKey { get; init; } = string.Empty;

    /// <summary>Tesseract language code: nld or eng.</summary>
    public string Language { get; init; } = "nld";
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
