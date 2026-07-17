using System.Globalization;
using RecipeLibrary.Web.Models;
using Xunit;

namespace RecipeLibrary.Web.ComponentTests;

public sealed class IngredientApiTests
{
    [Fact]
    public void SearchUrl_IncludesCultureQuery()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            var url = IngredientApi.SearchUrl("tomato");
            Assert.Contains("q=tomato", url, StringComparison.Ordinal);
            Assert.Contains("culture=en-US", url, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void MatchCommand_SetsCultureNameFromUi()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            var command = IngredientApi.MatchCommand("tomato");
            Assert.Equal("tomato", command.Input);
            Assert.Equal("en-US", command.CultureName);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
