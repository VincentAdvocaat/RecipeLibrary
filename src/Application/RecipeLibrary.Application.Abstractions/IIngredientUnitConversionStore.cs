using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Abstractions;

public interface IIngredientUnitConversionStore
{
    Task<IReadOnlyList<IngredientUnitConversion>> GetConversionsAsync(
        Guid canonicalIngredientId,
        Unit fromUnit,
        Unit toUnit,
        CancellationToken ct = default);

    Task<IngredientUnitConversionSuggestion?> GetPendingSuggestionAsync(
        Guid? canonicalIngredientId,
        string displayName,
        Unit fromUnit,
        Unit toUnit,
        CancellationToken ct = default);

    Task AddSuggestionAsync(IngredientUnitConversionSuggestion suggestion, CancellationToken ct = default);

    Task AddConversionAsync(IngredientUnitConversion conversion, CancellationToken ct = default);

    Task MarkSuggestionAcceptedAsync(Guid suggestionId, CancellationToken ct = default);

    Task<ConversionSource?> GetSourceByNameAsync(string name, CancellationToken ct = default);

    Task<Guid?> FindCanonicalIngredientIdByCatalogKeyAsync(string catalogKey, CancellationToken ct = default);
}

public interface IIngredientUnitConversionAiProposer
{
    Task<IngredientUnitConversionAiProposal?> ProposeAsync(
        IngredientUnitConversionAiRequest request,
        CancellationToken ct = default);
}

public sealed class IngredientUnitConversionAiRequest
{
    public required string IngredientName { get; init; }

    public required Unit FromUnit { get; init; }

    public decimal Quantity { get; init; }

    public string? SuggestedCatalogKey { get; init; }
}

public sealed class IngredientUnitConversionAiProposal
{
    public decimal AmountFrom { get; init; }

    public Unit FromUnit { get; init; }

    public decimal AmountTo { get; init; }

    public Unit ToUnit { get; init; }

    public string? SuggestedCatalogKey { get; init; }

    public string? Notes { get; init; }
}
