using System.Text;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.RecipeImport;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class RecipeImportBodyReaderTests
{
    [Fact]
    public async Task ReadUtf8UpToMaxBytesAsync_SoftTruncates_WithoutThrowing()
    {
        var payload = new string('a', 1000);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await RecipeImportBodyReader.ReadUtf8UpToMaxBytesAsync(stream, maxBytes: 100);

        Assert.True(result.Length <= 100);
        Assert.StartsWith("aaa", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Response exceeded", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadUtf8UpToMaxBytesAsync_ReturnsFullBody_WhenUnderLimit()
    {
        const string payload = "<html><body>small</body></html>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await RecipeImportBodyReader.ReadUtf8UpToMaxBytesAsync(stream, maxBytes: 10_000);

        Assert.Equal(payload, result);
    }
}

public sealed class BloggerDynamicContentTests
{
    [Fact]
    public void IsDynamicShell_DetectsBloggerDynamicTemplate()
    {
        var html = ReadFixture("dynamic-blogger-shell.html");

        Assert.True(BloggerDynamicContent.IsDynamicShell(html));
        Assert.True(BloggerDynamicContent.TryGetPostId(html, out var postId));
        Assert.Equal("7934533815331171322", postId);
    }

    [Fact]
    public void BuildAtomFeedUri_UsesSameOriginAndPostId()
    {
        var page = new Uri("https://bumbicurry.blogspot.com/2025/03/chickpea-butter-masala-chole-butter.html");
        var feed = BloggerDynamicContent.BuildAtomFeedUri(page, "7934533815331171322");

        Assert.Equal(
            "https://bumbicurry.blogspot.com/feeds/posts/default/7934533815331171322",
            feed.ToString());
    }

    [Fact]
    public void TryExtractHtmlFromAtom_ReturnsDecodedEntryContent()
    {
        var atom = ReadFixture("blogger-post.atom");

        var html = BloggerDynamicContent.TryExtractHtmlFromAtom(atom);

        Assert.NotNull(html);
        Assert.Contains("200 g chickpeas", html, StringComparison.Ordinal);
        Assert.Contains("Heat the oil.", html, StringComparison.Ordinal);
        Assert.Equal("Demo Curry Recipe", BloggerDynamicContent.TryGetEntryTitleFromAtom(atom));
    }

    [Fact]
    public void ShellToWrappedHtml_CanBeParsedByHtmlExtractor()
    {
        var atom = ReadFixture("blogger-post.atom");
        var entryHtml = BloggerDynamicContent.TryExtractHtmlFromAtom(atom);
        Assert.NotNull(entryHtml);

        var document = BloggerDynamicContent.WrapAsHtmlDocument(
            entryHtml,
            BloggerDynamicContent.TryGetEntryTitleFromAtom(atom));

        var text = new HtmlRecipeTextExtractor().Extract(document);

        Assert.Contains("chickpeas", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Heat the oil", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadFixture(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "recipe-import", fileName));
}
