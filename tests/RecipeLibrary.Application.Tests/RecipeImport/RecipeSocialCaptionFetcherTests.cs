using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Infrastructure.RecipeImport;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class RecipeSocialCaptionFetcherTests
{
    private const string ShortsUrl = "https://www.youtube.com/shorts/DSGRNoSTvLg";
    private const string VideoId = "DSGRNoSTvLg";

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("UNSET", false)]
    [InlineData("unset", false)]
    [InlineData("AIzaSyTestKey123", true)]
    public void TryGetConfiguredYouTubeApiKey_FiltersPlaceholderAndEmpty(string? input, bool expected)
    {
        var ok = RecipeSocialCaptionFetcher.TryGetConfiguredYouTubeApiKey(input, out var key);
        Assert.Equal(expected, ok);
        if (expected)
        {
            Assert.Equal(input!.Trim(), key);
        }
    }

    [Fact]
    public void TryParseYouTubeDataApiSnippet_PrefersDescriptionOverTitle()
    {
        var json = """
            {
              "items": [
                {
                  "snippet": {
                    "title": "Panang Curry #shorts",
                    "description": "Panang Curry\nIngredients\n* 1 tbsp fish sauce"
                  }
                }
              ]
            }
            """;

        var text = RecipeSocialCaptionFetcher.TryParseYouTubeDataApiSnippet(json);
        Assert.Equal("Panang Curry\nIngredients\n* 1 tbsp fish sauce", text);
    }

    [Fact]
    public void TryParseYouTubeDataApiSnippet_FallsBackToTitleWhenDescriptionEmpty()
    {
        var json = """
            {
              "items": [
                {
                  "snippet": {
                    "title": "Panang Curry #shorts",
                    "description": "   "
                  }
                }
              ]
            }
            """;

        var text = RecipeSocialCaptionFetcher.TryParseYouTubeDataApiSnippet(json);
        Assert.Equal("Panang Curry #shorts", text);
    }

    [Fact]
    public void TryParseYouTubeDataApiSnippet_ReturnsNullWhenItemsEmpty()
    {
        Assert.Null(RecipeSocialCaptionFetcher.TryParseYouTubeDataApiSnippet("""{"items":[]}"""));
    }

    [Fact]
    public void TryParseYouTubeInnerTubeDescription_ReadsShortDescription()
    {
        var json = """
            {
              "videoDetails": {
                "title": "Title only",
                "shortDescription": "Full recipe body"
              }
            }
            """;

        Assert.Equal("Full recipe body", RecipeSocialCaptionFetcher.TryParseYouTubeInnerTubeDescription(json));
    }

    [Fact]
    public void TryParseYouTubeInnerTubeDescription_ReturnsNullWithoutVideoDetails()
    {
        Assert.Null(RecipeSocialCaptionFetcher.TryParseYouTubeInnerTubeDescription("""{"playabilityStatus":{"status":"UNPLAYABLE"}}"""));
    }

    [Fact]
    public async Task TryFetchCaptionAsync_UsesDataApiWhenKeyConfigured()
    {
        var handler = new ScriptedHandler(request =>
        {
            if (request.Method == HttpMethod.Get
                && request.RequestUri!.Host.Contains("googleapis.com", StringComparison.OrdinalIgnoreCase)
                && request.RequestUri.Query.Contains("id=" + VideoId, StringComparison.Ordinal))
            {
                Assert.Contains("key=AIzaSyTestKey", request.RequestUri.Query, StringComparison.Ordinal);
                return JsonResponse("""
                    {"items":[{"snippet":{"title":"T","description":"From Data API"}}]}
                    """);
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var sut = CreateSut(handler, youtubeApiKey: "AIzaSyTestKey");
        var caption = await sut.TryFetchCaptionAsync(ShortsUrl);

        Assert.Equal("From Data API", caption);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task TryFetchCaptionAsync_FallsBackToInnerTubeWhenDataApiFails()
    {
        var handler = new ScriptedHandler(request =>
        {
            if (request.RequestUri!.Host.Contains("googleapis.com", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            if (request.Method == HttpMethod.Post
                && request.RequestUri.AbsolutePath.Contains("youtubei", StringComparison.OrdinalIgnoreCase))
            {
                return JsonResponse("""
                    {"videoDetails":{"title":"T","shortDescription":"From InnerTube"}}
                    """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var sut = CreateSut(handler, youtubeApiKey: "AIzaSyTestKey");
        var caption = await sut.TryFetchCaptionAsync(ShortsUrl);

        Assert.Equal("From InnerTube", caption);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task TryFetchCaptionAsync_FallsBackToInnerTubeWhenDataApiThrows()
    {
        var handler = new ScriptedHandler(request =>
        {
            if (request.RequestUri!.Host.Contains("googleapis.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException("Simulated Data API transport failure");
            }

            if (request.Method == HttpMethod.Post
                && request.RequestUri.AbsolutePath.Contains("youtubei", StringComparison.OrdinalIgnoreCase))
            {
                return JsonResponse("""
                    {"videoDetails":{"shortDescription":"From InnerTube after throw"}}
                    """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var sut = CreateSut(handler, youtubeApiKey: "AIzaSyTestKey");
        var caption = await sut.TryFetchCaptionAsync(ShortsUrl);

        Assert.Equal("From InnerTube after throw", caption);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task TryFetchCaptionAsync_FallsBackToInnerTubeWhenDataApiReturnsMalformedJson()
    {
        var handler = new ScriptedHandler(request =>
        {
            if (request.RequestUri!.Host.Contains("googleapis.com", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{not-json", Encoding.UTF8, "application/json"),
                };
            }

            if (request.Method == HttpMethod.Post
                && request.RequestUri.AbsolutePath.Contains("youtubei", StringComparison.OrdinalIgnoreCase))
            {
                return JsonResponse("""
                    {"videoDetails":{"shortDescription":"From InnerTube after bad JSON"}}
                    """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var sut = CreateSut(handler, youtubeApiKey: "AIzaSyTestKey");
        var caption = await sut.TryFetchCaptionAsync(ShortsUrl);

        Assert.Equal("From InnerTube after bad JSON", caption);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task TryFetchCaptionAsync_SkipsDataApiWhenKeyUnset()
    {
        var handler = new ScriptedHandler(request =>
        {
            if (request.RequestUri!.Host.Contains("googleapis.com", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"items":[{"snippet":{"description":"Should not be used"}}]}"""),
                };
            }

            if (request.Method == HttpMethod.Post)
            {
                return JsonResponse("""
                    {"videoDetails":{"shortDescription":"From InnerTube only"}}
                    """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var sut = CreateSut(handler, youtubeApiKey: "UNSET");
        var caption = await sut.TryFetchCaptionAsync(ShortsUrl);

        Assert.Equal("From InnerTube only", caption);
        Assert.Equal(1, handler.RequestCount);
        Assert.DoesNotContain(
            handler.RequestedUris,
            u => u.Host.Contains("googleapis.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryFetchCaptionAsync_ReturnsNullWhenAllYouTubeSourcesFail()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var sut = CreateSut(handler, youtubeApiKey: "AIzaSyTestKey");

        var caption = await sut.TryFetchCaptionAsync(ShortsUrl);

        Assert.Null(caption);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task TryFetchCaptionAsync_RecoversInstagramLinesFromPublicEmbed()
    {
        var handler = new ScriptedHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/embed/captioned/", StringComparison.Ordinal)
            ? HtmlResponse("""<div class="Caption"><a class="CaptionUsername">author</a>Mixed bean stew<br>Ingredients<br>300 g beans<br>Method<br>Simmer until tender.<div class="CaptionComments">Viewer comments</div></div>""")
            : new HttpResponseMessage(HttpStatusCode.Forbidden));

        var caption = await CreateSut(handler, null).TryFetchCaptionAsync("https://www.instagram.com/reel/ABC_123/?share=abc");

        Assert.Equal("Mixed bean stew\nIngredients\n300 g beans\nMethod\nSimmer until tender.", caption);
        Assert.Equal(2, handler.RequestCount);
        Assert.All(handler.RequestedUris, uri => Assert.Empty(uri.Query));
    }

    [Fact]
    public void InstagramCaption_DecodesJsonFreePublicMetadata_AndKeepsLineBreaks()
    {
        var html = """
            <meta name="description" content="11 likes, 2 comments - author on November 20, 2026: &quot;Pear compote
            Ingredients
            2 pears
            Method
            Simmer in water.&quot;.">
            """;

        Assert.Equal("Pear compote\nIngredients\n2 pears\nMethod\nSimmer in water.", PublicRecipeCaption.Instagram(html));
    }

    [Fact]
    public async Task TryFetchCaptionAsync_UsesVerifiedYouTubeCreatorCommentForMissingSteps()
    {
        var handler = new ScriptedHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/commentThreads", StringComparison.Ordinal)
            ? JsonResponse("""
                {"items":[
                  {"snippet":{"topLevelComment":{"snippet":{"authorChannelId":{"value":"viewer"},"textDisplay":"Method\nUse an unrelated sauce."}}}},
                  {"snippet":{"topLevelComment":{"snippet":{"authorChannelId":{"value":"creator"},"textDisplay":"Method\nSimmer the carrots for 8 minutes."}}}}
                ]}
                """)
            : JsonResponse("""
                {"items":[{"snippet":{"channelId":"creator","title":"Carrot side","description":"Carrot side\nIngredients\n2 carrots"}}]}
                """));

        var caption = await CreateSut(handler, "example-key").TryFetchCaptionAsync(ShortsUrl);

        Assert.Equal("Carrot side\nIngredients\n2 carrots\n\nMethod\nSimmer the carrots for 8 minutes.", caption);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task TryFetchCaptionAsync_RecoversYouTubeDescriptionFromCorrectPublicWatchPage()
    {
        var handler = new ScriptedHandler(request => request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.BadRequest)
            : HtmlResponse("""
                <script>var ytInitialPlayerResponse = {"videoDetails":{"videoId":"unrelated","shortDescription":"Wrong video"}};</script>
                <script>var ytInitialPlayerResponse = {"videoDetails":{"videoId":"DSGRNoSTvLg","shortDescription":"Root vegetable soup\nIngredients\n2 carrots"}}; after();</script>
                """));

        var caption = await CreateSut(handler, null).TryFetchCaptionAsync(ShortsUrl);

        Assert.Equal("Root vegetable soup\nIngredients\n2 carrots", caption);
        Assert.Equal("/watch", handler.RequestedUris[^1].AbsolutePath);
    }

    private static RecipeSocialCaptionFetcher CreateSut(HttpMessageHandler handler, string? youtubeApiKey)
    {
        var factory = new FixedHttpClientFactory(handler);
        var options = Options.Create(new RecipeImportOptions
        {
            YouTube = new RecipeImportYouTubeOptions { ApiKey = youtubeApiKey },
            UrlFetch = new RecipeImportUrlFetchOptions { MaxBytes = 2_097_152, TimeoutSeconds = 30 },
        });
        return new RecipeSocialCaptionFetcher(factory, options);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static HttpResponseMessage HtmlResponse(string html) =>
        new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

    private sealed class FixedHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
    }

    private sealed class ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public List<Uri> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (request.RequestUri is not null)
            {
                RequestedUris.Add(request.RequestUri);
            }

            return Task.FromResult(responder(request));
        }
    }
}
