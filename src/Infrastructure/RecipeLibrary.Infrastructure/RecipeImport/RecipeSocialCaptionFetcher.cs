using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.RecipeImport;

/// <summary>
/// Reads recipe captions from Instagram oEmbed and YouTube (Data API v3, then InnerTube).
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
        var oembedUrl =
            "https://www.instagram.com/api/v1/oembed/?url=" + Uri.EscapeDataString(canonicalPostUrl);

        var json = await SendGetAsync(oembedUrl, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return TryParseInstagramOEmbedTitle(json);
    }

    private async Task<string?> FetchYouTubeDescriptionAsync(string videoId, CancellationToken ct)
    {
        if (TryGetConfiguredYouTubeApiKey(options.Value.YouTube.ApiKey, out var apiKey))
        {
            try
            {
                var fromDataApi = await FetchYouTubeDataApiDescriptionAsync(videoId, apiKey, ct);
                if (!string.IsNullOrWhiteSpace(fromDataApi))
                {
                    return fromDataApi;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Data API timeout/network/malformed JSON: continue to InnerTube.
            }
        }

        return await FetchYouTubeInnerTubeDescriptionAsync(videoId, ct);
    }

    private async Task<string?> FetchYouTubeDataApiDescriptionAsync(
        string videoId,
        string apiKey,
        CancellationToken ct)
    {
        var endpoint =
            "https://www.googleapis.com/youtube/v3/videos"
            + "?part=snippet"
            + "&id=" + Uri.EscapeDataString(videoId)
            + "&key=" + Uri.EscapeDataString(apiKey);

        var json = await SendGetAsync(endpoint, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return TryParseYouTubeDataApiSnippet(json);
    }

    private async Task<string?> FetchYouTubeInnerTubeDescriptionAsync(string videoId, CancellationToken ct)
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

        var json = await SendPostAsync(endpoint, payload, "application/json", ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return TryParseYouTubeInnerTubeDescription(json);
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
        if (!document.RootElement.TryGetProperty("title", out var titleElement)
            || titleElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var caption = titleElement.GetString();
        return string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
    }

    internal static string? TryParseYouTubeDataApiSnippet(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array
            || items.GetArrayLength() == 0)
        {
            return null;
        }

        var first = items[0];
        if (!first.TryGetProperty("snippet", out var snippet)
            || snippet.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (snippet.TryGetProperty("description", out var descriptionElement)
            && descriptionElement.ValueKind == JsonValueKind.String)
        {
            var description = descriptionElement.GetString();
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description.Trim();
            }
        }

        if (snippet.TryGetProperty("title", out var titleElement)
            && titleElement.ValueKind == JsonValueKind.String)
        {
            var title = titleElement.GetString();
            return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        }

        return null;
    }

    internal static string? TryParseYouTubeInnerTubeDescription(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("videoDetails", out var details)
            || details.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (details.TryGetProperty("shortDescription", out var descriptionElement)
            && descriptionElement.ValueKind == JsonValueKind.String)
        {
            var description = descriptionElement.GetString();
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description.Trim();
            }
        }

        if (details.TryGetProperty("title", out var titleElement)
            && titleElement.ValueKind == JsonValueKind.String)
        {
            var title = titleElement.GetString();
            return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        }

        return null;
    }

    private async Task<string?> SendGetAsync(string url, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(RecipeImportServiceRegistration.HttpClientName);
        var endpoint = await RecipeImportUrlSafety.ResolvePublicHttpEndpointAsync(url, ct);

        using (RecipeImportConnectPin.Use(endpoint.Uri.Host, endpoint.Addresses))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint.Uri);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await ReadBodyAsync(response, ct);
        }
    }

    private async Task<string?> SendPostAsync(
        string url,
        string jsonBody,
        string contentType,
        CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(RecipeImportServiceRegistration.HttpClientName);
        var endpoint = await RecipeImportUrlSafety.ResolvePublicHttpEndpointAsync(url, ct);

        using (RecipeImportConnectPin.Use(endpoint.Uri.Host, endpoint.Addresses))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Uri);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd("application/json");
            request.Content = new StringContent(jsonBody, Encoding.UTF8, contentType);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await ReadBodyAsync(response, ct);
        }
    }

    private async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var maxBytes = options.Value.UrlFetch.MaxBytes;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[Math.Min(maxBytes, 8192)];
        var builder = new StringBuilder();
        int read;
        var totalBytes = 0;

        while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            totalBytes += Encoding.UTF8.GetByteCount(buffer.AsSpan(0, read));
            if (totalBytes > maxBytes)
            {
                // Oversized bodies are treated as fetch failure (TryFetchCaptionAsync → null).
                return string.Empty;
            }

            builder.Append(buffer, 0, read);
        }

        return builder.ToString();
    }
}
