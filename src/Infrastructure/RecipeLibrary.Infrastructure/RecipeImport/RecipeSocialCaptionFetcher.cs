using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.RecipeImport;

/// <summary>
/// Reads public recipe text from supported social post formats and APIs.
/// </summary>
public sealed class RecipeSocialCaptionFetcher(
    IHttpClientFactory httpClientFactory,
    IOptions<RecipeImportOptions> options) : IRecipeSocialCaptionFetcher
{
    private const string UserAgent = "RecipeLibrary/1.0 (+recipe-import)";
    private const string YouTubeClientVersion = "2.20240101.00.00";
    private const string KeyVaultPlaceholder = "UNSET";

    public async Task<string?> TryFetchCaptionAsync(string url, CancellationToken ct = default)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return null;
            }

            if (SocialMediaRecipeUrls.TryGetInstagramCanonicalPostUrl(uri, out var canonicalIg))
            {
                return await FetchInstagramCaptionAsync(canonicalIg, ct);
            }

            if (SocialMediaRecipeUrls.TryGetYouTubeVideoId(uri, out var videoId))
            {
                return await FetchYouTubeDescriptionAsync(videoId, ct);
            }

            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Transient network/timeout/oversize/malformed JSON: let callers fall back to HTML.
            return null;
        }
    }

    private async Task<string?> FetchInstagramCaptionAsync(string canonicalPostUrl, CancellationToken ct)
    {
        var page = await SendGetAsync(canonicalPostUrl, "text/html", ct);
        var caption = page is null ? null : PublicRecipeCaption.Instagram(page);
        if (PublicRecipeCaption.HasIngredientsAndSteps(caption))
        {
            return caption;
        }

        var embedPage = await SendGetAsync(canonicalPostUrl + "embed/captioned/", "text/html", ct);
        var embeddedCaption = embedPage is null ? null : PublicRecipeCaption.Instagram(embedPage);
        if (PublicRecipeCaption.HasIngredientsAndSteps(embeddedCaption)
            || (embeddedCaption?.Length ?? 0) > (caption?.Length ?? 0))
        {
            return embeddedCaption;
        }

        if (caption is not null)
        {
            return caption;
        }

        var oembedUrl = "https://www.instagram.com/api/v1/oembed/?url=" + Uri.EscapeDataString(canonicalPostUrl);
        var json = await SendGetAsync(oembedUrl, "application/json", ct);
        return json is null ? null : TryParseInstagramOEmbedTitle(json);
    }

    private async Task<string?> FetchYouTubeDescriptionAsync(string videoId, CancellationToken ct)
    {
        string? title = null;
        if (TryGetConfiguredYouTubeApiKey(options.Value.YouTube.ApiKey, out var apiKey))
        {
            try
            {
                var json = await SendGetAsync(YouTubeApiUrl("videos", $"part=snippet&id={videoId}", apiKey), "application/json", ct);
                if (json is not null)
                {
                    using var document = JsonDocument.Parse(json);
                    if (GetDataApiVideoSnippet(document.RootElement) is { } snippet)
                    {
                        title = ReadString(snippet, "title");
                        var description = ReadString(snippet, "description");
                        if (!PublicRecipeCaption.HasIngredientsAndSteps(description)
                            && ReadString(snippet, "channelId") is { } channelId)
                        {
                            var combined = await TryGetCreatorRecipeAsync(videoId, apiKey, channelId, title, description, ct);
                            if (combined is not null)
                            {
                                return combined;
                            }
                        }

                        if (description is not null)
                        {
                            return description;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // A malformed response may still have a public video page with valid metadata.
            }
        }

        var player = await FetchYouTubeInnerTubeDescriptionAsync(videoId, ct);
        if (player.Description is not null)
        {
            return player.Description;
        }

        var page = await SendGetAsync($"https://www.youtube.com/watch?v={videoId}", "text/html", ct);
        return (page is null ? null : PublicRecipeCaption.YouTube(page, videoId)) ?? title ?? player.Title;
    }

    private async Task<string?> TryGetCreatorRecipeAsync(
        string videoId, string apiKey, string channelId, string? title, string? description, CancellationToken ct)
    {
        var payload = await SendGetAsync(
            YouTubeApiUrl("commentThreads", $"part=snippet&videoId={videoId}&maxResults=20&order=relevance&textFormat=plainText", apiKey),
            "application/json", ct);
        if (payload is null)
        {
            return null;
        }

        try
        {
            return FindCreatorRecipe(payload, channelId, title, description);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string? FindCreatorRecipe(string json, string channelId, string? title, string? description)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (!TryGetObject(item, "snippet", out var thread)
                || !TryGetObject(thread, "topLevelComment", out var comment)
                || !TryGetObject(comment, "snippet", out var snippet)
                || !TryGetObject(snippet, "authorChannelId", out var author)
                || ReadString(author, "value") != channelId
                || ReadString(snippet, "textDisplay") is not { } text)
            {
                continue;
            }

            if (PublicRecipeCaption.HasIngredientsAndSteps(text))
            {
                return string.IsNullOrWhiteSpace(title) ? text : $"{title}\n\n{text}";
            }

            var combined = $"{description}\n\n{text}".Trim();
            if (PublicRecipeCaption.HasIngredientsAndSteps(combined))
            {
                return combined;
            }
        }

        return null;
    }

    private static string YouTubeApiUrl(string resource, string parameters, string apiKey) =>
        $"https://www.googleapis.com/youtube/v3/{resource}?{parameters}&key={Uri.EscapeDataString(apiKey)}";

    private async Task<(string? Description, string? Title)> FetchYouTubeInnerTubeDescriptionAsync(string videoId, CancellationToken ct)
    {
        var endpoint = "https://www.youtube.com/youtubei/v1/player?prettyPrint=false";
        var payload = JsonSerializer.Serialize(new
        {
            context = new
            {
                client = new
                {
                    clientName = "WEB",
                    clientVersion = YouTubeClientVersion,
                    hl = "en",
                    gl = "US",
                },
            },
            videoId,
        });

        var json = await SendAsync(HttpMethod.Post, endpoint, "application/json", payload, ct);
        if (json is null)
        {
            return default;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return TryGetObject(document.RootElement, "videoDetails", out var details)
                ? (ReadString(details, "shortDescription"), ReadString(details, "title"))
                : default;
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>True when the key is usable (not empty and not the Key Vault placeholder).</summary>
    internal static bool TryGetConfiguredYouTubeApiKey(string? apiKey, out string configuredKey)
    {
        configuredKey = string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        var trimmed = apiKey.Trim();
        if (trimmed.Equals(KeyVaultPlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        configuredKey = trimmed;
        return true;
    }

    internal static string? TryParseInstagramOEmbedTitle(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ReadString(document.RootElement, "title");
    }

    internal static string? TryParseYouTubeDataApiSnippet(string json)
    {
        using var document = JsonDocument.Parse(json);
        return GetDataApiVideoSnippet(document.RootElement) is { } snippet
            ? ReadYouTubeText(snippet, "description")
            : null;
    }

    private static JsonElement? GetDataApiVideoSnippet(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("items", out var items)
        && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0
        && TryGetObject(items[0], "snippet", out var snippet)
            ? snippet
            : null;

    internal static string? TryParseYouTubeInnerTubeDescription(string json)
    {
        using var document = JsonDocument.Parse(json);
        return TryGetObject(document.RootElement, "videoDetails", out var details)
            ? ReadYouTubeText(details, "shortDescription")
            : null;
    }

    internal static string? ReadYouTubeText(JsonElement snippet, string descriptionName) =>
        ReadString(snippet, descriptionName) ?? ReadString(snippet, "title");

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String && value.GetString()?.Trim() is { Length: > 0 } text
            ? text
            : null;

    private static bool TryGetObject(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out value)
            && value.ValueKind == JsonValueKind.Object;
    }

    private Task<string?> SendGetAsync(string url, string accept, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, url, accept, null, ct);

    private async Task<string?> SendAsync(HttpMethod method, string url, string accept, string? payload, CancellationToken ct)
    {
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(options.Value.UrlFetch.TimeoutSeconds));
        var token = requestTimeout.Token;
        try
        {
            using var client = httpClientFactory.CreateClient(RecipeImportServiceRegistration.HttpClientName);
            var endpoint = await RecipeImportUrlSafety.ResolvePublicHttpEndpointAsync(url, token);

            using (RecipeImportConnectPin.Use(endpoint.Uri.Host, endpoint.Addresses))
            {
                using var request = new HttpRequestMessage(method, endpoint.Uri);
                request.Headers.UserAgent.ParseAdd(UserAgent);
                request.Headers.Accept.ParseAdd(accept);
                request.Headers.AcceptLanguage.ParseAdd("en");
                if (payload is not null)
                {
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                }

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(token);
                var body = await RecipeImportBodyReader.ReadUtf8UpToMaxBytesAsync(stream, options.Value.UrlFetch.MaxBytes, token);
                return accept == "text/html" || !body.WasTruncated ? body.Text : null;
            }
        }
        catch (Exception error) when (!ct.IsCancellationRequested
            && error is HttpRequestException or IOException or OperationCanceledException or InvalidOperationException)
        {
            return null;
        }
    }
}
