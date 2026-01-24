namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// One step in the preparation of a recipe.
/// </summary>
public sealed class InstructionStep
{
    public Guid Id { get; set; }

    public Guid RecipeId { get; set; }

    public int StepNumber { get; set; }

    public string Text { get; set; } = string.Empty;
}

