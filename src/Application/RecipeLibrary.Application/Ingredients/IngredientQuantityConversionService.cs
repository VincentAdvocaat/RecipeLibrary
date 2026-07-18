using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Ingredients;

/// <summary>
/// Orchestrates kitchen→physical conversion: preferred curated row, pending suggestion, then AI.
/// </summary>
public sealed class IngredientQuantityConversionService(
    IIngredientUnitConversionStore store,
    IIngredientUnitConversionAiProposer aiProposer,
    IOptions<RecipeImportOptions> importOptions,
    ILogger<IngredientQuantityConversionService> logger)
{
    public static readonly string[] SourcePriority =
    [
        ConversionSourceNames.KingArthur,
        ConversionSourceNames.Usda,
        ConversionSourceNames.Manual,
    ];

    public async Task<IngredientQuantityConversionResult> ConvertToMassAsync(
        IngredientQuantityConversionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!UnitClassification.IsKitchenMeasure(request.FromUnit) || request.Quantity <= 0)
        {
            return IngredientQuantityConversionResult.Unavailable();
        }

        if (request.CanonicalIngredientId is Guid ingredientId)
        {
            var preferred = await GetPreferredConversionAsync(ingredientId, request.FromUnit, Unit.Gram, ct);
            if (preferred is not null)
            {
                return BuildResult(request.Quantity, preferred.AmountFrom, preferred.AmountTo, Unit.Gram, preferred.Origin.ToString());
            }
        }

        var pending = await store.GetPendingSuggestionAsync(
            request.CanonicalIngredientId,
            request.IngredientDisplayName,
            request.FromUnit,
            Unit.Gram,
            ct);
        if (pending is not null)
        {
            return BuildResult(request.Quantity, pending.AmountFrom, pending.AmountTo, Unit.Gram, "AiSuggestion");
        }

        var aiEnabled = importOptions.Value.Ai.Enabled
            && !string.IsNullOrWhiteSpace(importOptions.Value.Ai.ApiKey);
        if (!aiEnabled)
        {
            return IngredientQuantityConversionResult.Unavailable();
        }

        try
        {
            var proposal = await aiProposer.ProposeAsync(
                new IngredientUnitConversionAiRequest
                {
                    IngredientName = request.IngredientDisplayName,
                    FromUnit = request.FromUnit,
                    Quantity = request.Quantity,
                },
                ct);

            if (proposal is null
                || proposal.AmountFrom <= 0
                || proposal.AmountTo <= 0
                || proposal.ToUnit != Unit.Gram
                || proposal.FromUnit != request.FromUnit)
            {
                return IngredientQuantityConversionResult.Failed();
            }

            var suggestion = new IngredientUnitConversionSuggestion
            {
                Id = Guid.NewGuid(),
                CanonicalIngredientId = request.CanonicalIngredientId,
                IngredientDisplayName = request.IngredientDisplayName.Trim(),
                FromUnit = proposal.FromUnit,
                ToUnit = proposal.ToUnit,
                AmountFrom = proposal.AmountFrom,
                AmountTo = proposal.AmountTo,
                Status = ConversionSuggestionStatus.Pending,
                Model = importOptions.Value.Ai.Model,
                Notes = proposal.Notes,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await store.AddSuggestionAsync(suggestion, ct);

            if (request.CanonicalIngredientId is Guid matchedId)
            {
                await AcceptAsAiConversionAsync(suggestion, matchedId, ct);
            }

            return BuildResult(request.Quantity, proposal.AmountFrom, proposal.AmountTo, Unit.Gram, "AiSuggestion");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AI unit conversion failed for '{Name}' ({Unit}).",
                request.IngredientDisplayName,
                request.FromUnit);
            return IngredientQuantityConversionResult.Failed();
        }
    }

    public async Task<IngredientUnitConversion?> GetPreferredConversionAsync(
        Guid canonicalIngredientId,
        Unit fromUnit,
        Unit toUnit,
        CancellationToken ct = default)
    {
        var rows = await store.GetConversionsAsync(canonicalIngredientId, fromUnit, toUnit, ct);
        if (rows.Count == 0)
        {
            return null;
        }

        IngredientUnitConversion? best = null;
        var bestRank = int.MaxValue;

        foreach (var row in rows)
        {
            var sourceRank = Array.FindIndex(
                SourcePriority,
                n => string.Equals(n, row.ConversionSource.Name, StringComparison.OrdinalIgnoreCase));
            if (sourceRank < 0)
            {
                sourceRank = SourcePriority.Length;
            }

            // Manual curated before Manual AiAccepted
            var originBoost = row.Origin == ConversionOrigin.Curated ? 0 : 1;
            var rank = sourceRank * 10 + originBoost;
            if (rank < bestRank)
            {
                bestRank = rank;
                best = row;
            }
        }

        return best;
    }

    private async Task AcceptAsAiConversionAsync(
        IngredientUnitConversionSuggestion suggestion,
        Guid canonicalIngredientId,
        CancellationToken ct)
    {
        var manual = await store.GetSourceByNameAsync(ConversionSourceNames.Manual, ct);
        if (manual is null)
        {
            return;
        }

        var existing = await store.GetConversionsAsync(
            canonicalIngredientId,
            suggestion.FromUnit,
            suggestion.ToUnit,
            ct);
        if (existing.Any(c => c.ConversionSourceId == manual.Id))
        {
            await store.MarkSuggestionAcceptedAsync(suggestion.Id, ct);
            return;
        }

        await store.AddConversionAsync(
            new IngredientUnitConversion
            {
                Id = Guid.NewGuid(),
                CanonicalIngredientId = canonicalIngredientId,
                FromUnit = suggestion.FromUnit,
                ToUnit = suggestion.ToUnit,
                AmountFrom = suggestion.AmountFrom,
                AmountTo = suggestion.AmountTo,
                ConversionSourceId = manual.Id,
                Origin = ConversionOrigin.AiAccepted,
                ExternalReference = suggestion.Model,
                Notes = suggestion.Notes,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            ct);
        await store.MarkSuggestionAcceptedAsync(suggestion.Id, ct);
    }

    private static IngredientQuantityConversionResult BuildResult(
        decimal quantity,
        decimal amountFrom,
        decimal amountTo,
        Unit toUnit,
        string provenance)
    {
        var converted = quantity * (amountTo / amountFrom);
        return new IngredientQuantityConversionResult
        {
            Succeeded = true,
            Quantity = converted,
            Unit = toUnit,
            Provenance = provenance,
        };
    }
}

public sealed class IngredientQuantityConversionRequest
{
    public Guid? CanonicalIngredientId { get; init; }

    public required string IngredientDisplayName { get; init; }

    public required Unit FromUnit { get; init; }

    public decimal Quantity { get; init; }
}

public sealed class IngredientQuantityConversionResult
{
    public bool Succeeded { get; init; }

    public bool IsUnavailable { get; init; }

    public decimal Quantity { get; init; }

    public Unit Unit { get; init; }

    public string? Provenance { get; init; }

    public static IngredientQuantityConversionResult Unavailable() =>
        new() { Succeeded = false, IsUnavailable = true };

    public static IngredientQuantityConversionResult Failed() =>
        new() { Succeeded = false, IsUnavailable = false };
}
