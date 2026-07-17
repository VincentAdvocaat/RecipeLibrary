using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class IngredientLanguageFallbackTests
{
    [Theory]
    [InlineData("nl-NL", "nl-NL", "nl", "en")]
    [InlineData("nl", "nl", "en")]
    [InlineData("en-US", "en-US", "en")]
    [InlineData("en", "en")]
    [InlineData("nl-BE", "nl-BE", "nl", "en")]
    public void ResolveChain_FollowsParentThenEnglish(string culture, params string[] expected)
    {
        var chain = IngredientLanguageFallback.ResolveChain(culture);
        Assert.Equal(expected, chain);
    }

    [Fact]
    public void ResolveChain_AddsEnglishWhenCultureMissing()
    {
        var chain = IngredientLanguageFallback.ResolveChain(null);
        Assert.Equal(["en"], chain);
    }

    [Fact]
    public void DisplayResolver_FallsBackToCatalogKey()
    {
        var ingredient = new CanonicalIngredient
        {
            Id = Guid.NewGuid(),
            CatalogKey = "tomato",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var display = IngredientDisplayResolver.Resolve(ingredient, ["nl", "en"]);
        Assert.Equal("tomato", display.DisplayName);
        Assert.Null(display.LanguageCode);
    }

    [Fact]
    public void DisplayResolver_PrefersRequestedLanguage()
    {
        var ingredient = IngredientTestFactory.Create("tomaat", "nl", catalogKey: "tomato");
        ingredient.Translations.Add(new IngredientTranslation
        {
            Id = Guid.NewGuid(),
            IngredientId = ingredient.Id,
            LanguageCode = "en",
            DisplayName = "tomato",
            NormalizedDisplayName = "tomato",
        });

        var nl = IngredientDisplayResolver.Resolve(ingredient, ["nl", "en"]);
        Assert.Equal("tomaat", nl.DisplayName);
        Assert.Equal("nl", nl.LanguageCode);

        var en = IngredientDisplayResolver.Resolve(ingredient, ["en"]);
        Assert.Equal("tomato", en.DisplayName);
        Assert.Equal("en", en.LanguageCode);
    }
}
