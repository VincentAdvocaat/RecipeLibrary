namespace RecipeLibrary.Application.Contracts;

public sealed class CreateRecipeRequest
{
    public string Title { get; init; } = string.Empty;

    public int PreparationTimeMinutes { get; init; }

    public List<IngredientDto> Ingredients { get; init; } = [];

    public List<InstructionStepDto> InstructionSteps { get; init; } = [];
}

public sealed class IngredientDto
{
    public string Name { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    /// <summary>
    /// Unit name, e.g. "Gram", "Milliliter", "Piece".
    /// Parsed to the Domain <c>Unit</c> enum in the application layer.
    /// </summary>
    public string Unit { get; init; } = string.Empty;
}

public sealed class InstructionStepDto
{
    public int StepNumber { get; init; }

    public string Text { get; init; } = string.Empty;
}

