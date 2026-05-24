using RecipeLibrary.Application.Ingredients;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class IngredientListDisplayTests
{
    [Fact]
    public void FormatNameWithPreparation_AppendsPreparationInParentheses()
    {
        var result = IngredientListDisplay.FormatNameWithPreparation("Ui", "fijn gesneden");

        Assert.Equal("Ui (fijn gesneden)", result);
    }

    [Fact]
    public void FormatNameWithPreparation_ReturnsName_WhenPreparationMissing()
    {
        var result = IngredientListDisplay.FormatNameWithPreparation("Ui", null);

        Assert.Equal("Ui", result);
    }
}
