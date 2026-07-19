using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class IngredientQuantityConversionServiceTests
{
    [Fact]
    public async Task ConvertToMassAsync_UsesPreferredKingArthurOverUsda()
    {
        var ingredientId = Guid.NewGuid();
        var store = new FakeStore
        {
            Conversions =
            [
                CreateConversion(ingredientId, ConversionSourceNames.Usda, 125m, ConversionOrigin.Curated),
                CreateConversion(ingredientId, ConversionSourceNames.KingArthur, 120m, ConversionOrigin.Curated),
            ],
        };

        var sut = CreateSut(store, aiEnabled: false);
        var result = await sut.ConvertToMassAsync(new IngredientQuantityConversionRequest
        {
            CanonicalIngredientId = ingredientId,
            IngredientDisplayName = "flour",
            FromUnit = Unit.Cup,
            Quantity = 2m,
        });

        Assert.True(result.Succeeded);
        Assert.Equal(240m, result.Quantity);
        Assert.Equal(Unit.Gram, result.Unit);
    }

    [Fact]
    public async Task ConvertToMassAsync_ReusesPendingSuggestion_BeforeAi()
    {
        var store = new FakeStore
        {
            Pending = new IngredientUnitConversionSuggestion
            {
                Id = Guid.NewGuid(),
                IngredientDisplayName = "mystery spice",
                FromUnit = Unit.Teaspoon,
                ToUnit = Unit.Gram,
                AmountFrom = 1,
                AmountTo = 3,
                Status = ConversionSuggestionStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
            },
        };
        var ai = new FakeAi { Called = false };

        var sut = CreateSut(store, aiEnabled: true, ai);
        var result = await sut.ConvertToMassAsync(new IngredientQuantityConversionRequest
        {
            IngredientDisplayName = "Mystery Spice",
            FromUnit = Unit.Teaspoon,
            Quantity = 2m,
        });

        Assert.True(result.Succeeded);
        Assert.Equal(6m, result.Quantity);
        Assert.False(ai.Called);
    }

    [Fact]
    public async Task ConvertToMassAsync_AiStoresPendingSuggestion_WithoutCatalogAccept()
    {
        var ingredientId = Guid.NewGuid();
        var store = new FakeStore
        {
            ManualSourceId = Guid.NewGuid(),
        };
        var ai = new FakeAi
        {
            Proposal = new IngredientUnitConversionAiProposal
            {
                AmountFrom = 1,
                FromUnit = Unit.Cup,
                AmountTo = 190,
                ToUnit = Unit.Gram,
            },
        };

        var sut = CreateSut(store, aiEnabled: true, ai);
        var result = await sut.ConvertToMassAsync(new IngredientQuantityConversionRequest
        {
            CanonicalIngredientId = ingredientId,
            IngredientDisplayName = "rice",
            FromUnit = Unit.Cup,
            Quantity = 1m,
        });

        Assert.True(result.Succeeded);
        Assert.Equal(190m, result.Quantity);
        Assert.Equal("AiSuggestion", result.Provenance);
        Assert.Single(store.AddedSuggestions);
        Assert.Equal(ConversionSuggestionStatus.Pending, store.AddedSuggestions[0].Status);
        Assert.Equal("rice", store.AddedSuggestions[0].IngredientDisplayName);
        Assert.Empty(store.AddedConversions);
    }

    [Fact]
    public async Task ConvertToMassAsync_AiAddOrGet_ReturnsExistingAmounts_OnDuplicatePending()
    {
        var existingId = Guid.NewGuid();
        var store = new FakeStore();
        // Simulate a raced insert that already won uniqueness before this AI proposal is stored.
        store.SeedPending(new IngredientUnitConversionSuggestion
        {
            Id = existingId,
            IngredientDisplayName = "mystery spice",
            FromUnit = Unit.Teaspoon,
            ToUnit = Unit.Gram,
            AmountFrom = 1,
            AmountTo = 3,
            Status = ConversionSuggestionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        // Clear the pre-AI pending lookup so Convert falls through to AI, then AddOrGet.
        store.ClearPendingLookup();

        var ai = new FakeAi
        {
            Proposal = new IngredientUnitConversionAiProposal
            {
                AmountFrom = 1,
                FromUnit = Unit.Teaspoon,
                AmountTo = 99,
                ToUnit = Unit.Gram,
            },
        };

        var sut = CreateSut(store, aiEnabled: true, ai);
        var result = await sut.ConvertToMassAsync(new IngredientQuantityConversionRequest
        {
            IngredientDisplayName = "Mystery Spice",
            FromUnit = Unit.Teaspoon,
            Quantity = 2m,
        });

        Assert.True(result.Succeeded);
        Assert.True(ai.Called);
        Assert.Equal(6m, result.Quantity); // 2 * (3/1) from existing pending, not 99
        Assert.Empty(store.AddedSuggestions);
        Assert.Equal(existingId, store.LastAddOrGetReturned!.Id);
    }

    [Fact]
    public async Task HasEstimateSourceAsync_False_WhenNoSeedPendingOrAi()
    {
        var store = new FakeStore();
        var sut = CreateSut(store, aiEnabled: false);
        var available = await sut.HasEstimateSourceAsync(new IngredientQuantityConversionRequest
        {
            CanonicalIngredientId = Guid.NewGuid(),
            IngredientDisplayName = "unknown spice",
            FromUnit = Unit.Teaspoon,
            Quantity = 1m,
        });

        Assert.False(available);
    }

    [Fact]
    public async Task HasEstimateSourceAsync_True_WhenAiEnabled()
    {
        var store = new FakeStore();
        var sut = CreateSut(store, aiEnabled: true);
        var available = await sut.HasEstimateSourceAsync(new IngredientQuantityConversionRequest
        {
            IngredientDisplayName = "mystery spice",
            FromUnit = Unit.Teaspoon,
            Quantity = 1m,
        });

        Assert.True(available);
    }

    [Fact]
    public async Task GetConvertibleKeysAsync_BatchesWithoutPerItemStoreCalls_WhenAiEnabled()
    {
        var store = new FakeStore();
        var sut = CreateSut(store, aiEnabled: true);
        var keys = await sut.GetConvertibleKeysAsync(
        [
            ("a", new IngredientQuantityConversionRequest
            {
                IngredientDisplayName = "a",
                FromUnit = Unit.Cup,
                Quantity = 1,
            }),
            ("b", new IngredientQuantityConversionRequest
            {
                IngredientDisplayName = "b",
                FromUnit = Unit.Teaspoon,
                Quantity = 1,
            }),
            ("c", new IngredientQuantityConversionRequest
            {
                IngredientDisplayName = "c",
                FromUnit = Unit.Gram,
                Quantity = 100,
            }),
        ]);

        Assert.Equal(2, keys.Count);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
        Assert.DoesNotContain("c", keys);
        Assert.Equal(0, store.ConversionBatchCalls);
        Assert.Equal(0, store.PendingBatchCalls);
    }

    [Fact]
    public async Task GetConvertibleKeysAsync_UsesBatchLookups_WhenAiDisabled()
    {
        var ingredientId = Guid.NewGuid();
        var store = new FakeStore
        {
            Conversions =
            [
                CreateConversion(ingredientId, ConversionSourceNames.Usda, 185m, ConversionOrigin.Curated),
            ],
        };
        var sut = CreateSut(store, aiEnabled: false);
        var keys = await sut.GetConvertibleKeysAsync(
        [
            ("rice", new IngredientQuantityConversionRequest
            {
                CanonicalIngredientId = ingredientId,
                IngredientDisplayName = "rice",
                FromUnit = Unit.Cup,
                Quantity = 1,
            }),
            ("unknown", new IngredientQuantityConversionRequest
            {
                IngredientDisplayName = "mystery",
                FromUnit = Unit.Cup,
                Quantity = 1,
            }),
        ]);

        Assert.Contains("rice", keys);
        Assert.DoesNotContain("unknown", keys);
        Assert.Equal(1, store.ConversionBatchCalls);
        Assert.Equal(1, store.PendingBatchCalls);
    }

    [Fact]
    public async Task GetPreferredConversion_PrefersCuratedManualOverAiAccepted()
    {
        var ingredientId = Guid.NewGuid();
        var store = new FakeStore
        {
            Conversions =
            [
                CreateConversion(ingredientId, ConversionSourceNames.Manual, 200m, ConversionOrigin.AiAccepted),
                CreateConversion(ingredientId, ConversionSourceNames.Manual, 185m, ConversionOrigin.Curated),
            ],
        };

        var sut = CreateSut(store, aiEnabled: false);
        var preferred = await sut.GetPreferredConversionAsync(ingredientId, Unit.Cup, Unit.Gram);
        Assert.NotNull(preferred);
        Assert.Equal(185m, preferred!.AmountTo);
        Assert.Equal(ConversionOrigin.Curated, preferred.Origin);
    }

    private static IngredientUnitConversion CreateConversion(
        Guid ingredientId,
        string sourceName,
        decimal amountTo,
        ConversionOrigin origin) =>
        new()
        {
            Id = Guid.NewGuid(),
            CanonicalIngredientId = ingredientId,
            FromUnit = Unit.Cup,
            ToUnit = Unit.Gram,
            AmountFrom = 1,
            AmountTo = amountTo,
            ConversionSourceId = Guid.NewGuid(),
            Origin = origin,
            CreatedAt = DateTimeOffset.UtcNow,
            ConversionSource = new ConversionSource { Id = Guid.NewGuid(), Name = sourceName },
        };

    private static IngredientQuantityConversionService CreateSut(
        FakeStore store,
        bool aiEnabled,
        FakeAi? ai = null) =>
        new(
            store,
            ai ?? new FakeAi(),
            Options.Create(new RecipeImportOptions
            {
                Ai = new RecipeImportAiOptions
                {
                    Enabled = aiEnabled,
                    ApiKey = aiEnabled ? "test-key" : null,
                    Model = "gpt-test",
                },
            }),
            NullLogger<IngredientQuantityConversionService>.Instance);

    private sealed class FakeAi : IIngredientUnitConversionAiProposer
    {
        public bool Called { get; set; }

        public IngredientUnitConversionAiProposal? Proposal { get; set; }

        public Task<IngredientUnitConversionAiProposal?> ProposeAsync(
            IngredientUnitConversionAiRequest request,
            CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(Proposal);
        }
    }

    private sealed class FakeStore : IIngredientUnitConversionStore
    {
        private readonly List<IngredientUnitConversionSuggestion> _pendingRows = [];
        private bool _includePendingInLookup = true;

        public List<IngredientUnitConversion> Conversions { get; init; } = [];

        public IngredientUnitConversionSuggestion? Pending
        {
            get => _pendingRows.FirstOrDefault();
            init
            {
                if (value is not null)
                {
                    _pendingRows.Add(CloneNormalized(value));
                }
            }
        }

        public Guid ManualSourceId { get; init; } = Guid.NewGuid();

        public List<IngredientUnitConversionSuggestion> AddedSuggestions { get; } = [];

        public List<IngredientUnitConversion> AddedConversions { get; } = [];

        public IngredientUnitConversionSuggestion? LastAddOrGetReturned { get; private set; }

        public int ConversionBatchCalls { get; private set; }

        public int PendingBatchCalls { get; private set; }

        public void SeedPending(IngredientUnitConversionSuggestion suggestion) =>
            _pendingRows.Add(CloneNormalized(suggestion));

        public void ClearPendingLookup() => _includePendingInLookup = false;

        public Task<IReadOnlyList<IngredientUnitConversion>> GetConversionsAsync(
            Guid canonicalIngredientId,
            Unit fromUnit,
            Unit toUnit,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<IngredientUnitConversion>>(
                Conversions.Where(c =>
                    c.CanonicalIngredientId == canonicalIngredientId
                    && c.FromUnit == fromUnit
                    && c.ToUnit == toUnit).ToList());

        public Task<IReadOnlyList<IngredientUnitConversion>> GetConversionsForIngredientsAsync(
            IReadOnlyCollection<Guid> canonicalIngredientIds,
            IReadOnlyCollection<Unit> fromUnits,
            Unit toUnit,
            CancellationToken ct = default)
        {
            ConversionBatchCalls++;
            return Task.FromResult<IReadOnlyList<IngredientUnitConversion>>(
                Conversions.Where(c =>
                    canonicalIngredientIds.Contains(c.CanonicalIngredientId)
                    && fromUnits.Contains(c.FromUnit)
                    && c.ToUnit == toUnit).ToList());
        }

        public Task<IngredientUnitConversionSuggestion?> GetPendingSuggestionAsync(
            Guid? canonicalIngredientId,
            string displayName,
            Unit fromUnit,
            Unit toUnit,
            CancellationToken ct = default)
        {
            if (!_includePendingInLookup)
            {
                return Task.FromResult<IngredientUnitConversionSuggestion?>(null);
            }

            return Task.FromResult(FindPending(canonicalIngredientId, displayName, fromUnit, toUnit));
        }

        public Task<IReadOnlyList<IngredientUnitConversionSuggestion>> GetPendingSuggestionsBatchAsync(
            IReadOnlyCollection<Guid> canonicalIngredientIds,
            IReadOnlyCollection<string> displayNamesWithoutCanonical,
            IReadOnlyCollection<Unit> fromUnits,
            Unit toUnit,
            CancellationToken ct = default)
        {
            PendingBatchCalls++;
            if (!_includePendingInLookup)
            {
                return Task.FromResult<IReadOnlyList<IngredientUnitConversionSuggestion>>([]);
            }

            var normalizedNames = displayNamesWithoutCanonical
                .Select(NormalizeDisplayName)
                .Where(static n => n.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var matches = _pendingRows.Where(p =>
                p.Status == ConversionSuggestionStatus.Pending
                && fromUnits.Contains(p.FromUnit)
                && p.ToUnit == toUnit
                && (
                    (p.CanonicalIngredientId is Guid id && canonicalIngredientIds.Contains(id))
                    || (p.CanonicalIngredientId is null && normalizedNames.Contains(p.IngredientDisplayName))
                )).ToList();

            return Task.FromResult<IReadOnlyList<IngredientUnitConversionSuggestion>>(matches);
        }

        public Task<IngredientUnitConversionSuggestion> AddOrGetPendingSuggestionAsync(
            IngredientUnitConversionSuggestion suggestion,
            CancellationToken ct = default)
        {
            suggestion.IngredientDisplayName = NormalizeDisplayName(suggestion.IngredientDisplayName);

            // Always consult stored rows (race/uniqueness), even when Convert skipped pre-AI lookup.
            var existing = FindPending(
                suggestion.CanonicalIngredientId,
                suggestion.IngredientDisplayName,
                suggestion.FromUnit,
                suggestion.ToUnit);
            if (existing is not null)
            {
                LastAddOrGetReturned = existing;
                return Task.FromResult(existing);
            }

            _pendingRows.Add(suggestion);
            AddedSuggestions.Add(suggestion);
            LastAddOrGetReturned = suggestion;
            return Task.FromResult(suggestion);
        }

        public Task AddConversionAsync(IngredientUnitConversion conversion, CancellationToken ct = default)
        {
            AddedConversions.Add(conversion);
            return Task.CompletedTask;
        }

        public Task MarkSuggestionAcceptedAsync(Guid suggestionId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<ConversionSource?> GetSourceByNameAsync(string name, CancellationToken ct = default) =>
            Task.FromResult<ConversionSource?>(
                string.Equals(name, ConversionSourceNames.Manual, StringComparison.OrdinalIgnoreCase)
                    ? new ConversionSource { Id = ManualSourceId, Name = ConversionSourceNames.Manual }
                    : null);

        public Task<Guid?> FindCanonicalIngredientIdByCatalogKeyAsync(string catalogKey, CancellationToken ct = default) =>
            Task.FromResult<Guid?>(null);

        private IngredientUnitConversionSuggestion? FindPending(
            Guid? canonicalIngredientId,
            string displayName,
            Unit fromUnit,
            Unit toUnit)
        {
            var normalizedName = NormalizeDisplayName(displayName);
            return _pendingRows.FirstOrDefault(p =>
                p.Status == ConversionSuggestionStatus.Pending
                && p.FromUnit == fromUnit
                && p.ToUnit == toUnit
                && (
                    canonicalIngredientId is Guid id
                        ? p.CanonicalIngredientId == id
                        : p.CanonicalIngredientId is null
                          && string.Equals(p.IngredientDisplayName, normalizedName, StringComparison.Ordinal)
                ));
        }

        private static IngredientUnitConversionSuggestion CloneNormalized(IngredientUnitConversionSuggestion value) =>
            new()
            {
                Id = value.Id,
                CanonicalIngredientId = value.CanonicalIngredientId,
                IngredientDisplayName = NormalizeDisplayName(value.IngredientDisplayName),
                FromUnit = value.FromUnit,
                ToUnit = value.ToUnit,
                AmountFrom = value.AmountFrom,
                AmountTo = value.AmountTo,
                Status = value.Status,
                Model = value.Model,
                Notes = value.Notes,
                ExternalReference = value.ExternalReference,
                CreatedAt = value.CreatedAt,
            };

        private static string NormalizeDisplayName(string? displayName) =>
            (displayName ?? string.Empty).Trim().ToLowerInvariant();
    }
}
