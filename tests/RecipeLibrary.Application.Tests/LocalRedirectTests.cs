using RecipeLibrary.Application.Security;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class LocalRedirectTests
{
    [Theory]
    [InlineData("/recipes", "/recipes")]
    [InlineData("/recipes/create?x=1", "/recipes/create?x=1")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.com", "/")]
    [InlineData("//evil.com", "/")]
    [InlineData("/\\evil", "/")]
    [InlineData("recipes", "/")]
    public void Normalize_BlocksOpenRedirects(string? input, string expected)
    {
        Assert.Equal(expected, LocalRedirect.Normalize(input));
    }

    [Fact]
    public void Normalize_UsesCustomFallback()
    {
        Assert.Equal("/recipes", LocalRedirect.Normalize("//evil.com", fallback: "/recipes"));
    }
}
