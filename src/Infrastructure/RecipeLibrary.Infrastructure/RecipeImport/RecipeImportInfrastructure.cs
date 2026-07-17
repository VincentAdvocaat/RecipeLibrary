using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

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
/// Optional AI ingredient-line parser for low-confidence deterministic lines.
/// </summary>
public sealed class OpenAiIngredientLineAiParser(
    IHttpClientFactory httpClientFactory,
    IOptions<RecipeImportOptions> options) : IIngredientLineAiParser
{
    private const string IngredientSystemPrompt = """
        Parse recipe ingredient lines (Dutch or English) into JSON.
        Return {"ingredients":[{"rawLine":"","quantity":null,"unit":null,"name":"","preparation":null,"confidence":0.9}]}.
        Rules:
        - unit must be one of: Gram, Milliliter, Teaspoon, Tablespoon, Piece, Clove, Handful, Slice, Sprig, Leaf, Bunch, Stalk; null when unmeasured
        - quantity may be a decimal (0.25, 0.5, 2.25, 3); null when unmeasured
        - preparation is null when absent; preserve parentheses and notes from the source line
        - when a line gives both weight and count (e.g. "390 g / 3 medium tomatoes"), prefer the count unit (Piece, Clove) and keep weight in preparation
        - name is the ingredient noun phrase only (lowercase is fine)
        - return one object per input line, in the same order
        """;

    private static readonly JsonSerializerOptions JsonOptions = OpenAiRecipeJson.Options;

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
            return [];
        }

        var content = await OpenAiRecipeJson.RequestJsonAsync(
            httpClientFactory,
            aiOptions,
            IngredientSystemPrompt,
            JsonSerializer.Serialize(new { ingredients = rawLines }, JsonOptions),
            ct);

        var parsed = JsonSerializer.Deserialize<AiIngredientBatchResponse>(content, JsonOptions)
            ?? throw new InvalidOperationException("AI parser returned invalid JSON.");

        if (parsed.Ingredients is null || parsed.Ingredients.Count == 0)
        {
            throw new InvalidOperationException("AI parser returned no ingredients.");
        }

        return parsed.Ingredients
            .Select((item, index) => MapIngredientItem(item, index < rawLines.Count ? rawLines[index] : item.RawLine))
            .ToList();
    }

    internal static AiParsedIngredientLine MapIngredientItem(AiIngredientItem item, string? rawLine) =>
        new()
        {
            RawLine = rawLine ?? item.RawLine ?? string.Empty,
            Quantity = NormalizeQuantity(item.Quantity),
            Unit = NormalizeUnit(item.Unit),
            Name = item.Name?.Trim() ?? string.Empty,
            Preparation = NormalizePreparation(item.Preparation),
            Confidence = item.Confidence <= 0 ? 0.85m : item.Confidence,
        };

    private static decimal? NormalizeQuantity(decimal? quantity) =>
        quantity is null or <= 0 ? null : quantity;

    private static string? NormalizeUnit(string? unit) =>
        string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();

    private static string? NormalizePreparation(string? preparation) =>
        string.IsNullOrWhiteSpace(preparation) ? null : preparation.Trim();

    private sealed class AiIngredientBatchResponse
    {
        public List<AiIngredientItem>? Ingredients { get; set; }
    }

    internal sealed class AiIngredientItem
    {
        public string? RawLine { get; set; }

        public decimal? Quantity { get; set; }

        public string? Unit { get; set; }

        public string? Name { get; set; }

        public string? Preparation { get; set; }

        public decimal Confidence { get; set; }
    }
}

/// <summary>
/// Parses a full normalized plain-text recipe via LLM.
/// </summary>
public sealed class OpenAiRecipeAiParser(
    IHttpClientFactory httpClientFactory,
    IOptions<RecipeImportOptions> options) : IRecipeAiParser
{
    private const string RecipeSystemPrompt = """
        Parse a normalized plain-text recipe (Dutch or English) into JSON.
        Return {
          "title": "",
          "description": null,
          "preparationTimeMinutes": null,
          "cookingTimeMinutes": null,
          "servings": null,
          "ingredients": [{"rawLine":"","quantity":null,"unit":null,"name":"","preparation":null,"confidence":0.9}],
          "steps": [{"stepNumber":1,"text":""}]
        }.
        Rules:
        - ingredient unit enum: Gram, Milliliter, Teaspoon, Tablespoon, Piece, Clove, Handful, Slice, Sprig, Leaf, Bunch, Stalk; null when unmeasured
        - quantity decimal allowed; null when unmeasured
        - prefer count units when both weight and count appear in one line
        - preparation null when absent; preserve parentheses from source
        - steps numbered from 1; use only the primary recipe (ignore blog comments, related posts, footers)
        - ignore content after Serving Suggestions / tips / comments when present
        """;

    private static readonly JsonSerializerOptions JsonOptions = OpenAiRecipeJson.Options;

    public async Task<ImportRecipeResult> ParseAsync(string plainText, CancellationToken ct = default)
    {
        var aiOptions = options.Value.Ai;
        if (!aiOptions.Enabled || string.IsNullOrWhiteSpace(aiOptions.ApiKey))
        {
            throw new InvalidOperationException("Full-recipe AI parsing is not configured.");
        }

        var content = await OpenAiRecipeJson.RequestJsonAsync(
            httpClientFactory,
            aiOptions,
            RecipeSystemPrompt,
            plainText,
            ct);

        var parsed = JsonSerializer.Deserialize<AiRecipeResponse>(content, JsonOptions)
            ?? throw new InvalidOperationException("AI recipe parser returned invalid JSON.");

        var ingredients = (parsed.Ingredients ?? [])
            .Select(item =>
            {
                var mapped = OpenAiIngredientLineAiParser.MapIngredientItem(item, item.RawLine);
                return new ImportedIngredientLine
                {
                    RawLine = mapped.RawLine,
                    Quantity = mapped.Quantity,
                    Unit = mapped.Unit,
                    Name = mapped.Name,
                    Preparation = mapped.Preparation,
                    Confidence = mapped.Confidence,
                    ParseMethod = ImportParseMethod.Ai,
                };
            })
            .ToList();

        var steps = (parsed.Steps ?? [])
            .Select((step, index) => new ImportedInstructionStep
            {
                StepNumber = step.StepNumber > 0 ? step.StepNumber : index + 1,
                Text = step.Text?.Trim() ?? string.Empty,
            })
            .Where(step => step.Text.Length > 0)
            .ToList();

        return new ImportRecipeResult
        {
            Title = parsed.Title?.Trim(),
            Description = string.IsNullOrWhiteSpace(parsed.Description) ? null : parsed.Description.Trim(),
            PreparationTimeMinutes = parsed.PreparationTimeMinutes,
            CookingTimeMinutes = parsed.CookingTimeMinutes,
            Servings = parsed.Servings,
            Source = ImportSource.PlainText,
            Ingredients = ingredients,
            Steps = steps,
            Warnings = [ImportWarningCodes.LowConfidenceAiHint],
        };
    }

    private sealed class AiRecipeResponse
    {
        public string? Title { get; set; }

        public string? Description { get; set; }

        public int? PreparationTimeMinutes { get; set; }

        public int? CookingTimeMinutes { get; set; }

        public int? Servings { get; set; }

        public List<OpenAiIngredientLineAiParser.AiIngredientItem>? Ingredients { get; set; }

        public List<AiStepItem>? Steps { get; set; }
    }

    private sealed class AiStepItem
    {
        public int StepNumber { get; set; }

        public string? Text { get; set; }
    }
}

internal static class OpenAiRecipeJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static async Task<string> RequestJsonAsync(
        IHttpClientFactory httpClientFactory,
        RecipeImportAiOptions aiOptions,
        string systemPrompt,
        string userContent,
        CancellationToken ct)
    {
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
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aiOptions.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, Options),
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

        return content;
    }
}

public sealed class NullIngredientLineAiParser : IIngredientLineAiParser
{
    public Task<IReadOnlyList<AiParsedIngredientLine>> ParseLinesAsync(
        IReadOnlyList<string> rawLines,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AiParsedIngredientLine>>([]);
}

public sealed class NullRecipeAiParser : IRecipeAiParser
{
    public Task<ImportRecipeResult> ParseAsync(string plainText, CancellationToken ct = default) =>
        throw new InvalidOperationException("Full-recipe AI parsing is not configured.");
}
