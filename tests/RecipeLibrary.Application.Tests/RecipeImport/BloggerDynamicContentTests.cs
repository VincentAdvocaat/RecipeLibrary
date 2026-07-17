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

        Assert.True(result.WasTruncated);
        Assert.True(Encoding.UTF8.GetByteCount(result.Text) <= 100);
        Assert.StartsWith("aaa", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Response exceeded", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadUtf8UpToMaxBytesAsync_ReturnsFullBody_WhenUnderLimit()
    {
        const string payload = "<html><body>small</body></html>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await RecipeImportBodyReader.ReadUtf8UpToMaxBytesAsync(stream, maxBytes: 10_000);

        Assert.False(result.WasTruncated);
        Assert.Equal(payload, result.Text);
    }

    [Fact]
    public async Task ReadUtf8UpToMaxBytesAsync_KeepsSurrogatePairsIntact()
    {
        // "😀" is one Unicode scalar (U+1F600) encoded as a UTF-16 surrogate pair and 4 UTF-8 bytes.
        var payload = "ab" + "😀" + "cd";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        // MaxBytes lands inside the emoji's UTF-8 sequence if we wrongly split the pair;
        // with pair-aware truncation we should keep "ab" only (2 bytes).
        var result = await RecipeImportBodyReader.ReadUtf8UpToMaxBytesAsync(stream, maxBytes: 3);

        Assert.True(result.WasTruncated);
        Assert.Equal("ab", result.Text);
        Assert.DoesNotContain(result.Text, c => char.IsSurrogate(c));
    }

    [Fact]
    public async Task ReadUtf8UpToMaxBytesAsync_MarksTruncated_WhenExactLimitFilled_AndMoreRemains()
    {
        var payload = new string('x', 200);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await RecipeImportBodyReader.ReadUtf8UpToMaxBytesAsync(stream, maxBytes: 100);

        Assert.True(result.WasTruncated);
        Assert.Equal(100, Encoding.UTF8.GetByteCount(result.Text));
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
    public void IsDynamicShell_RejectsBloggerTemplateWithoutDynamicContent()
    {
        const string html = """
            <html><head>
            <meta name="blogger-template" content="awesome"/>
            <meta name="description" content="This blog feels dynamic"/>
            </head><body>no post</body></html>
            """;

        Assert.False(BloggerDynamicContent.IsDynamicShell(html));
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

    [Fact]
    public async Task TryRecoverAsync_FetchesAtomAndWrapsDocument()
    {
        var shell = ReadFixture("dynamic-blogger-shell.html");
        var atom = ReadFixture("blogger-post.atom");
        Uri? requested = null;

        var recovered = await BloggerDynamicContent.TryRecoverAsync(
            shell,
            new Uri("https://example.blogspot.com/2025/03/chickpea-butter-masala.html"),
            (uri, _) =>
            {
                requested = uri;
                return Task.FromResult((atom, WasTruncated: false));
            });

        Assert.NotNull(recovered);
        Assert.False(recovered.WasTruncated);
        Assert.Equal(
            "https://example.blogspot.com/feeds/posts/default/7934533815331171322",
            requested!.ToString());
        Assert.Contains("200 g chickpeas", recovered.Html, StringComparison.Ordinal);
        Assert.Contains("<title>Demo Curry Recipe</title>", recovered.Html, StringComparison.Ordinal);

        var text = new HtmlRecipeTextExtractor().Extract(recovered.Html);
        Assert.Contains("Heat the oil", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryRecoverAsync_ReturnsNull_WhenNotDynamicShell()
    {
        var fetchCount = 0;

        var recovered = await BloggerDynamicContent.TryRecoverAsync(
            "<html><body>plain post</body></html>",
            new Uri("https://example.blogspot.com/post.html"),
            (_, _) =>
            {
                fetchCount++;
                return Task.FromResult(("<entry/>", false));
            });

        Assert.Null(recovered);
        Assert.Equal(0, fetchCount);
    }

    private static string ReadFixture(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "recipe-import", fileName));
}
