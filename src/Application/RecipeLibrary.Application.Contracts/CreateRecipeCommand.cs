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

    public string? Preparation { get; init; }

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
    /// RecipeCategory enum value; null means no category filter.
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

// --- Recipe detail query ---

public sealed class GetRecipeByIdQuery : IQuery<GetRecipeByIdResult?>
{
    public Guid RecipeId { get; init; }
}

public sealed class GetRecipeByIdResult
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public int PreparationMinutes { get; init; }
    public int CookingMinutes { get; init; }
    public int Category { get; init; }
    public IReadOnlyList<RecipeDetailIngredientItem> Ingredients { get; init; } = [];
    public IReadOnlyList<RecipeDetailStepItem> Steps { get; init; } = [];
}

public sealed class RecipeDetailIngredientItem
{
    public string Name { get; init; } = string.Empty;
    public string? Preparation { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
}

public sealed class RecipeDetailStepItem
{
    public int StepNumber { get; init; }
    public string Text { get; init; } = string.Empty;
}

// --- Recipe update / delete ---

public sealed class UpdateRecipeCommand : ICommand<UpdateRecipeResult>
{
    public Guid RecipeId { get; init; }
    public string Title { get; init; } = string.Empty;
    public int PreparationTimeMinutes { get; init; }
    public int CookingTimeMinutes { get; init; }
    public int Category { get; init; }
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public List<CreateRecipeIngredientDto> Ingredients { get; init; } = [];
    public List<CreateRecipeInstructionStepDto> InstructionSteps { get; init; } = [];
}

public sealed record UpdateRecipeResult(Guid RecipeId);

public sealed class DeleteRecipeCommand : ICommand<DeleteRecipeResult>
{
    public Guid RecipeId { get; init; }
}

public sealed record DeleteRecipeResult(bool Deleted);

public sealed class GetRecipeIngredientTagsQuery : IQuery<IReadOnlyList<string>>
{
    public Guid RecipeId { get; init; }
}
