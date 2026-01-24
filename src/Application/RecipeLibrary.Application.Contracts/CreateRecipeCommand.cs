namespace RecipeLibrary.Application.Contracts;

public sealed class CreateRecipeCommand : ICommand<CreateRecipeResult>
{
    public string Title { get; init; } = string.Empty;

    public int PreparationTimeMinutes { get; init; }

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

