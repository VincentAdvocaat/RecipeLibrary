using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class UnitAliasMapTests
{
    [Theory]
    [InlineData("g", Unit.Gram, 1)]
    [InlineData("kg", Unit.Gram, 1000)]
    [InlineData("ml", Unit.Milliliter, 1)]
    [InlineData("dl", Unit.Milliliter, 100)]
    [InlineData("tl", Unit.Teaspoon, 1)]
    [InlineData("el", Unit.Tablespoon, 1)]
    [InlineData("stuk", Unit.Piece, 1)]
    public void TryResolve_MapsAliases(string alias, Unit expectedUnit, decimal expectedMultiplier)
    {
        var success = UnitAliasMap.TryResolve(alias, out var match);

        Assert.True(success);
        Assert.Equal(expectedUnit, match.Unit);
        Assert.Equal(expectedMultiplier, match.Multiplier);
    }
}
