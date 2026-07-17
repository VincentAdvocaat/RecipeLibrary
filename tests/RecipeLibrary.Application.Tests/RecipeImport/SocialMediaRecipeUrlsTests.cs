using RecipeLibrary.Application.Abstractions;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class SocialMediaRecipeUrlsTests
{
    [Theory]
    [InlineData("https://www.instagram.com/reel/DYOlI0BhVdS/?igsh=abc", "https://www.instagram.com/reel/DYOlI0BhVdS/")]
    [InlineData("https://instagram.com/p/AbCdEfGhIjK/", "https://www.instagram.com/p/AbCdEfGhIjK/")]
    [InlineData("https://www.instagram.com/reels/XYZ123abcde/", "https://www.instagram.com/reel/XYZ123abcde/")]
    [InlineData("https://www.instagram.com/tv/ShortCode12/", "https://www.instagram.com/tv/ShortCode12/")]
    public void TryGetInstagramCanonicalPostUrl_NormalizesPath(string input, string expected)
    {
        Assert.True(SocialMediaRecipeUrls.TryGetInstagramCanonicalPostUrl(new Uri(input), out var canonical));
        Assert.Equal(expected, canonical);
    }

    [Theory]
    [InlineData("https://www.youtube.com/shorts/DSGRNoSTvLg", "DSGRNoSTvLg")]
    [InlineData("https://www.youtube.com/watch?v=DSGRNoSTvLg&t=12", "DSGRNoSTvLg")]
    [InlineData("https://youtu.be/DSGRNoSTvLg", "DSGRNoSTvLg")]
    [InlineData("https://m.youtube.com/embed/DSGRNoSTvLg", "DSGRNoSTvLg")]
    public void TryGetYouTubeVideoId_ExtractsId(string input, string expected)
    {
        Assert.True(SocialMediaRecipeUrls.TryGetYouTubeVideoId(new Uri(input), out var videoId));
        Assert.Equal(expected, videoId);
    }

    [Theory]
    [InlineData("https://example.com/recipe")]
    [InlineData("https://www.instagram.com/explore/")]
    [InlineData("https://www.youtube.com/feed/trending")]
    public void SocialDetection_RejectsNonPostUrls(string input)
    {
        var uri = new Uri(input);
        Assert.False(SocialMediaRecipeUrls.IsSocialRecipeUrl(uri));
    }
}
