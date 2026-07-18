using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// Immutable approved conversion for a canonical ingredient (culinary → physical).
/// </summary>
public sealed class IngredientUnitConversion
{
    public Guid Id { get; set; }

    public Guid CanonicalIngredientId { get; set; }

    public Unit FromUnit { get; set; }

    public Unit ToUnit { get; set; }

    public decimal AmountFrom { get; set; }

    public decimal AmountTo { get; set; }

    public Guid ConversionSourceId { get; set; }

    public ConversionOrigin Origin { get; set; }

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public CanonicalIngredient CanonicalIngredient { get; set; } = null!;

    public ConversionSource ConversionSource { get; set; } = null!;
}
