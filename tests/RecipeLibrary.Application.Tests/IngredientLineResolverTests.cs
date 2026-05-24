using RecipeLibrary.Application.Ingredients;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class IngredientLineResolverTests
{
    private readonly IngredientLineResolver _sut = new(new IngredientNameParser());

    [Fact]
    public void Resolve_UsesExplicitPreparation_AndStripsSuffixFromName()
    {
        var resolved = _sut.Resolve("ui fijn gesneden", "in blokjes");

        Assert.Equal("ui", resolved.DisplayName);
        Assert.Equal("in blokjes", resolved.Preparation);
    }

    [Fact]
    public void Resolve_ParsesPreparationFromName_WhenExplicitPreparationMissing()
    {
        var resolved = _sut.Resolve("gember geraspt", null);

        Assert.Equal("gember", resolved.DisplayName);
        Assert.Equal("geraspt", resolved.Preparation);
    }

    [Fact]
    public void Resolve_ReturnsNameOnly_WhenNoPreparation()
    {
        var resolved = _sut.Resolve("tomaten", null);

        Assert.Equal("tomaten", resolved.DisplayName);
        Assert.Null(resolved.Preparation);
    }
}
