namespace RecipeLibrary.Application.Contracts;

public sealed class CreateRecipeCommand : ICommand<CreateRecipeResult>
{
    public string Title { get; init; } = string.Empty;

    public int PreparationTimeMinutes { get; init; }

    public int CookingTimeMinutes { get; init; }

    public int Category { get; init; } // RecipeCategory value

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }

    public List<CreateRecipeIngredientDto> Ingredients { get; init; } = [];

    public List<CreateRecipeInstructionStepDto> InstructionSteps { get; init; } = [];
}

public sealed record CreateRecipeResult(Guid RecipeId);

public sealed class CreateRecipeIngredientDto
{
    public string Name { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    /// <summary>
    /// Unit name, e.g. "Gram", "Milliliter", "Piece".
    /// Parsed to the Domain <c>Unit</c> enum in the application layer.
    /// </summary>
    public string Unit { get; init; } = string.Empty;
}

public sealed class CreateRecipeInstructionStepDto
{
    public int StepNumber { get; init; }

    public string Text { get; init; } = string.Empty;
}

// --- Recipe list query (overview) ---

public sealed class GetRecipeListQuery : IQuery<GetRecipeListResult>
{
    public string? Search { get; init; }

    /// <summary>
    /// RecipeCategory enum value; null = "Alle" (no filter).
    /// </summary>
    public int? Category { get; init; }
}

public sealed record GetRecipeListResult(IReadOnlyList<RecipeOverviewItem> Items);

public sealed class RecipeOverviewItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public int PreparationMinutes { get; init; }
    public int CookingMinutes { get; init; }
    /// <summary>RecipeCategory enum value.</summary>
    public int Category { get; init; }
    public IReadOnlyList<string> IngredientNames { get; init; } = [];
}

// --- Recipe image upload (command) ---

public sealed class UploadRecipeImageCommand : ICommand<UploadRecipeImageResult>
{
    public required Stream Content { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
}

public sealed record UploadRecipeImageResult(string Url);

// --- Recipe image serve (query) ---

public sealed class GetRecipeImageQuery : IQuery<GetRecipeImageResult?>
{
    public required string StorageKey { get; init; }
}

public sealed record GetRecipeImageResult(Stream Stream, string ContentType);
