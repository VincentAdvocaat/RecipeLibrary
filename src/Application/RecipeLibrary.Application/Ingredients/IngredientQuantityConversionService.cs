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

        if (!IsAiFallbackAvailable)
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
                IngredientDisplayName = request.IngredientDisplayName,
                FromUnit = proposal.FromUnit,
                ToUnit = proposal.ToUnit,
                AmountFrom = proposal.AmountFrom,
                AmountTo = proposal.AmountTo,
                Status = ConversionSuggestionStatus.Pending,
                Model = importOptions.Value.Ai.Model,
                Notes = proposal.Notes,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            // Persist as Pending only — AI is a generator, not trusted catalog truth.
            var stored = await store.AddOrGetPendingSuggestionAsync(suggestion, ct);

            return BuildResult(request.Quantity, stored.AmountFrom, stored.AmountTo, Unit.Gram, "AiSuggestion");
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

    /// <summary>
    /// Batch: which item keys can use curated/pending rows or AI fallback (no AI call).
    /// </summary>
    public async Task<IReadOnlySet<string>> GetConvertibleKeysAsync(
        IReadOnlyList<(string Key, IngredientQuantityConversionRequest Request)> items,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var result = new HashSet<string>(StringComparer.Ordinal);
        var candidates = items
            .Where(x =>
                !string.IsNullOrEmpty(x.Key)
                && UnitClassification.IsKitchenMeasure(x.Request.FromUnit)
                && x.Request.Quantity > 0)
            .ToList();

        if (candidates.Count == 0)
        {
            return result;
        }

        // AI fallback covers every kitchen measure — skip per-row DB lookups.
        if (IsAiFallbackAvailable)
        {
            foreach (var (key, _) in candidates)
            {
                result.Add(key);
            }

            return result;
        }

        var ingredientIds = candidates
            .Select(x => x.Request.CanonicalIngredientId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var fromUnits = candidates
            .Select(x => x.Request.FromUnit)
            .Distinct()
            .ToList();

        var conversions = await store.GetConversionsForIngredientsAsync(
            ingredientIds,
            fromUnits,
            Unit.Gram,
            ct);
        var convertibleByIngredient = conversions
            .Select(c => (c.CanonicalIngredientId, c.FromUnit))
            .ToHashSet();

        var namesWithoutId = candidates
            .Where(x => x.Request.CanonicalIngredientId is null)
            .Select(x => x.Request.IngredientDisplayName)
            .ToList();

        var pendings = await store.GetPendingSuggestionsBatchAsync(
            ingredientIds,
            namesWithoutId,
            fromUnits,
            Unit.Gram,
            ct);

        foreach (var (key, request) in candidates)
        {
            if (request.CanonicalIngredientId is Guid id
                && convertibleByIngredient.Contains((id, request.FromUnit)))
            {
                result.Add(key);
                continue;
            }

            if (HasMatchingPending(pendings, request))
            {
                result.Add(key);
            }
        }

        return result;
    }

    /// <summary>
    /// True when convert can use a curated/pending row or AI fallback (no AI call).
    /// </summary>
    public async Task<bool> HasEstimateSourceAsync(
        IngredientQuantityConversionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!UnitClassification.IsKitchenMeasure(request.FromUnit) || request.Quantity <= 0)
        {
            return false;
        }

        if (IsAiFallbackAvailable)
        {
            return true;
        }

        if (request.CanonicalIngredientId is Guid ingredientId)
        {
            var preferred = await GetPreferredConversionAsync(ingredientId, request.FromUnit, Unit.Gram, ct);
            if (preferred is not null)
            {
                return true;
            }
        }

        var pending = await store.GetPendingSuggestionAsync(
            request.CanonicalIngredientId,
            request.IngredientDisplayName,
            request.FromUnit,
            Unit.Gram,
            ct);
        return pending is not null;
    }

    public bool IsAiFallbackAvailable =>
        importOptions.Value.Ai.Enabled
        && !string.IsNullOrWhiteSpace(importOptions.Value.Ai.ApiKey);

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

    private static bool HasMatchingPending(
        IReadOnlyList<IngredientUnitConversionSuggestion> pendings,
        IngredientQuantityConversionRequest request)
    {
        if (request.CanonicalIngredientId is Guid id)
        {
            return pendings.Any(p =>
                p.CanonicalIngredientId == id
                && p.FromUnit == request.FromUnit);
        }

        var name = request.IngredientDisplayName.Trim();
        return pendings.Any(p =>
            p.CanonicalIngredientId is null
            && p.FromUnit == request.FromUnit
            && string.Equals(p.IngredientDisplayName, name, StringComparison.OrdinalIgnoreCase));
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
