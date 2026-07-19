using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class UnitClassificationTests
{
    [Theory]
    [InlineData(Unit.Gram, UnitDimension.Mass)]
    [InlineData(Unit.Ounce, UnitDimension.Mass)]
    [InlineData(Unit.Pound, UnitDimension.Mass)]
    [InlineData(Unit.Milliliter, UnitDimension.Volume)]
    [InlineData(Unit.Cup, UnitDimension.Volume)]
    [InlineData(Unit.Piece, UnitDimension.Count)]
    public void GetDimension_MapsExpected(Unit unit, UnitDimension expected)
    {
        Assert.Equal(expected, UnitClassification.GetDimension(unit));
    }

    [Theory]
    [InlineData(Unit.Teaspoon, true)]
    [InlineData(Unit.Tablespoon, true)]
    [InlineData(Unit.Cup, true)]
    [InlineData(Unit.Gram, false)]
    [InlineData(Unit.Ounce, false)]
    public void IsKitchenMeasure_OnlyCulinaryVolume(Unit unit, bool expected)
    {
        Assert.Equal(expected, UnitClassification.IsKitchenMeasure(unit));
    }

    [Fact]
    public void AllowsCulinaryFractions_ExcludesOunce()
    {
        Assert.False(UnitRules.AllowsCulinaryFractions(Unit.Ounce));
        Assert.True(UnitRules.AllowsDecimalQuantity(Unit.Ounce));
    }
}

public sealed class MassUnitConverterTests
{
    [Fact]
    public void ToGrams_FromOunce()
    {
        Assert.Equal(28.349523125m, MassUnitConverter.ToGrams(1m, Unit.Ounce));
    }

    [Fact]
    public void RoundTrip_Pound()
    {
        var grams = MassUnitConverter.ToGrams(2m, Unit.Pound);
        Assert.Equal(2m, MassUnitConverter.FromGrams(grams, Unit.Pound));
    }
}

public sealed class IngredientMeasurePresenterTests
{
    [Fact]
    public void ApplyMassPresentation_Metric_ConvertsOunceToGram()
    {
        var (qty, unit) = IngredientMeasurePresenter.ApplyMassPresentation(1m, Unit.Ounce, MeasureSystem.Metric);
        Assert.Equal(Unit.Gram, unit);
        Assert.Equal(28m, qty);
    }

    [Fact]
    public void ApplyMassPresentation_Imperial_KeepsKitchenUnchanged()
    {
        var (qty, unit) = IngredientMeasurePresenter.ApplyMassPresentation(0.5m, Unit.Cup, MeasureSystem.Imperial);
        Assert.Equal(Unit.Cup, unit);
        Assert.Equal(0.5m, qty);
    }

    [Fact]
    public void ApplyMassPreferenceToImported_ConvertsOunceToGram_UnderMetric()
    {
        var (qty, unit) = IngredientMeasurePresenter.ApplyMassPreferenceToImported(
            8m,
            "Ounce",
            MeasureSystem.Metric);

        Assert.Equal(227m, qty);
        Assert.Equal(nameof(Unit.Gram), unit);
    }

    [Fact]
    public void ApplyMassPreferenceToImported_ConvertsGramToOunce_UnderImperial()
    {
        var (qty, unit) = IngredientMeasurePresenter.ApplyMassPreferenceToImported(
            100m,
            "Gram",
            MeasureSystem.Imperial);

        Assert.Equal(3.53m, qty);
        Assert.Equal(nameof(Unit.Ounce), unit);
    }

    [Fact]
    public void ApplyMassPreferenceToImported_LeavesKitchenMeasureUnchanged()
    {
        var (qty, unit) = IngredientMeasurePresenter.ApplyMassPreferenceToImported(
            1m,
            "Cup",
            MeasureSystem.Metric);

        Assert.Equal(1m, qty);
        Assert.Equal("Cup", unit);
    }

    [Fact]
    public void MeasureSystemDefaults_IsMetricIndependentOfCulture()
    {
        Assert.Equal(MeasureSystem.Metric, MeasureSystemDefaults.Default);
    }
}

public sealed class UnitRulesSelectableTests
{
    [Fact]
    public void SelectableUnitNamesFor_Metric_HidesImperialMassUnlessCurrent()
    {
        var names = UnitRules.SelectableUnitNamesFor(MeasureSystem.Metric);
        Assert.Contains(nameof(Unit.Gram), names);
        Assert.DoesNotContain(nameof(Unit.Ounce), names);
        Assert.DoesNotContain(nameof(Unit.Pound), names);
        Assert.Contains(nameof(Unit.Cup), names);

        var withCurrent = UnitRules.SelectableUnitNamesFor(MeasureSystem.Metric, nameof(Unit.Ounce));
        Assert.Contains(nameof(Unit.Ounce), withCurrent);
    }

    [Fact]
    public void SelectableUnitNamesFor_Imperial_HidesGramUnlessCurrent()
    {
        var names = UnitRules.SelectableUnitNamesFor(MeasureSystem.Imperial);
        Assert.Contains(nameof(Unit.Ounce), names);
        Assert.Contains(nameof(Unit.Pound), names);
        Assert.DoesNotContain(nameof(Unit.Gram), names);

        var withCurrent = UnitRules.SelectableUnitNamesFor(MeasureSystem.Imperial, nameof(Unit.Gram));
        Assert.Contains(nameof(Unit.Gram), withCurrent);
    }
}
