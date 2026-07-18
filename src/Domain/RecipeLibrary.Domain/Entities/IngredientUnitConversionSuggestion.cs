using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// AI-proposed conversion candidate (not preferred catalog truth until accepted).
/// </summary>
public sealed class IngredientUnitConversionSuggestion
{
    public Guid Id { get; set; }

    public Guid? CanonicalIngredientId { get; set; }

    public string IngredientDisplayName { get; set; } = string.Empty;

    public Unit FromUnit { get; set; }

    public Unit ToUnit { get; set; }

    public decimal AmountFrom { get; set; }

    public decimal AmountTo { get; set; }

    public ConversionSuggestionStatus Status { get; set; }

    public string? Model { get; set; }

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public CanonicalIngredient? CanonicalIngredient { get; set; }
}
