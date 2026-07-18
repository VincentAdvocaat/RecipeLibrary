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
            IngredientDisplayName = "mystery spice",
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
        Assert.Empty(store.AddedConversions);
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
        public List<IngredientUnitConversion> Conversions { get; init; } = [];

        public IngredientUnitConversionSuggestion? Pending { get; init; }

        public Guid ManualSourceId { get; init; } = Guid.NewGuid();

        public List<IngredientUnitConversionSuggestion> AddedSuggestions { get; } = [];

        public List<IngredientUnitConversion> AddedConversions { get; } = [];

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

        public Task<IngredientUnitConversionSuggestion?> GetPendingSuggestionAsync(
            Guid? canonicalIngredientId,
            string displayName,
            Unit fromUnit,
            Unit toUnit,
            CancellationToken ct = default) =>
            Task.FromResult(Pending);

        public Task AddSuggestionAsync(IngredientUnitConversionSuggestion suggestion, CancellationToken ct = default)
        {
            AddedSuggestions.Add(suggestion);
            return Task.CompletedTask;
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
    }
}
