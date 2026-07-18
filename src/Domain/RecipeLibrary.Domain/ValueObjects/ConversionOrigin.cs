namespace RecipeLibrary.Domain.ValueObjects;

/// <summary>
/// How an approved <see cref="Entities.IngredientUnitConversion"/> entered the catalog.
/// </summary>
public enum ConversionOrigin
{
    Curated = 1,
    AiAccepted = 2,
}
