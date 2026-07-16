using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.RecipeImport;

public sealed class RecipeImportContentFetcher(
    IHttpClientFactory httpClientFactory,
    IOptions<RecipeImportOptions> options) : IRecipeImportContentFetcher
{
    private const int MaxRedirects = 5;

    public async Task<string> FetchHtmlAsync(string url, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient(RecipeImportServiceRegistration.HttpClientName);
        var current = await RecipeImportUrlSafety.ResolvePublicHttpEndpointAsync(url, ct);

        for (var redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            using (RecipeImportConnectPin.Use(current.Uri.Host, current.Addresses))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current.Uri);
                request.Headers.UserAgent.ParseAdd("RecipeLibrary/1.0 (+recipe-import)");

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    var location = response.Headers.Location;
                    if (location is null)
                    {
                        throw new InvalidOperationException("Redirect response did not include a Location header.");
                    }

                    var next = location.IsAbsoluteUri
                        ? location
                        : new Uri(current.Uri, location);

                    current = await RecipeImportUrlSafety.ResolvePublicHttpEndpointAsync(next, ct);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Failed to fetch URL: {(int)response.StatusCode} {response.ReasonPhrase}");
                }

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
                        throw new InvalidOperationException($"Response exceeded maximum size of {maxBytes} bytes.");
                    }

                    builder.Append(buffer, 0, read);
                }

                return builder.ToString();
            }
        }

        throw new InvalidOperationException($"Too many redirects while fetching URL (max {MaxRedirects}).");
    }
}

/// <summary>
/// Optional AI ingredient-line parser. Not used by the unified plain-text import pipeline;
/// kept for future low-confidence upgrade paths.
/// </summary>
public sealed class OpenAiIngredientLineAiParser(
    IHttpClientFactory httpClientFactory,
    IOptions<RecipeImportOptions> options) : IIngredientLineAiParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<IReadOnlyList<AiParsedIngredientLine>> ParseLinesAsync(
        IReadOnlyList<string> rawLines,
        CancellationToken ct = default)
    {
        if (rawLines.Count == 0)
        {
            return [];
        }

        var aiOptions = options.Value.Ai;
        if (!aiOptions.Enabled || string.IsNullOrWhiteSpace(aiOptions.ApiKey))
        {
            return rawLines.Select(line => new AiParsedIngredientLine
            {
                RawLine = line,
                Quantity = 1,
                Unit = "Piece",
                Name = line,
                Confidence = 0.3m,
            }).ToList();
        }

        var client = httpClientFactory.CreateClient(RecipeImportServiceRegistration.HttpClientName);
        var endpoint = string.IsNullOrWhiteSpace(aiOptions.Endpoint)
            ? "https://api.openai.com/v1/chat/completions"
            : aiOptions.Endpoint.TrimEnd('/');

        if (!endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            endpoint += "/chat/completions";
        }

        var payload = new
        {
            model = aiOptions.Model,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = """
                        Parse Dutch recipe ingredient lines into JSON.
                        Return {"ingredients":[{"rawLine":"","quantity":0,"unit":"Gram|Milliliter|Teaspoon|Tablespoon|Piece","name":"","preparation":null,"confidence":0.0}]}.
                        Use enum unit names exactly. quantity must be a whole number. preparation is optional notes like cutting style or 'naar smaak'.
                        """,
                },
                new
                {
                    role = "user",
                    content = JsonSerializer.Serialize(new { ingredients = rawLines }, JsonOptions),
                },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aiOptions.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"AI parser request failed: {(int)response.StatusCode} {body}");
        }

        using var document = JsonDocument.Parse(body);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("AI parser returned empty content.");
        }

        var parsed = JsonSerializer.Deserialize<AiIngredientBatchResponse>(content, JsonOptions);
        if (parsed?.Ingredients is null || parsed.Ingredients.Count == 0)
        {
            throw new InvalidOperationException("AI parser returned no ingredients.");
        }

        return parsed.Ingredients
            .Select((item, index) => new AiParsedIngredientLine
            {
                RawLine = index < rawLines.Count ? rawLines[index] : item.RawLine ?? string.Empty,
                Quantity = item.Quantity <= 0 ? 1 : decimal.Round(item.Quantity, 0, MidpointRounding.AwayFromZero),
                Unit = string.IsNullOrWhiteSpace(item.Unit) ? "Piece" : item.Unit,
                Name = item.Name ?? string.Empty,
                Preparation = item.Preparation,
                Confidence = item.Confidence <= 0 ? 0.8m : item.Confidence,
            })
            .ToList();
    }

    private sealed class AiIngredientBatchResponse
    {
        public List<AiIngredientItem>? Ingredients { get; set; }
    }

    private sealed class AiIngredientItem
    {
        public string? RawLine { get; set; }

        public decimal Quantity { get; set; }

        public string? Unit { get; set; }

        public string? Name { get; set; }

        public string? Preparation { get; set; }

        public decimal Confidence { get; set; }
    }
}

public sealed class NullIngredientLineAiParser : IIngredientLineAiParser
{
    public Task<IReadOnlyList<AiParsedIngredientLine>> ParseLinesAsync(
        IReadOnlyList<string> rawLines,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AiParsedIngredientLine>>([]);
}
