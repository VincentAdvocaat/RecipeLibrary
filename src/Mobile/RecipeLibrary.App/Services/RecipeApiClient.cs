using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RecipeLibrary.App.Services;

public sealed class RecipeApiClient
{
    public const string ClientId = "maui-app";

    private readonly HttpClient _http;
    private string? _accessToken;

    public RecipeApiClient(HttpClient http) => _http = http;

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);

    public async Task LoginAsync(string userName, string password, CancellationToken ct = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = userName,
            ["password"] = password,
            ["client_id"] = ClientId,
            ["scope"] = "api offline_access",
        });

        using var response = await _http.PostAsync("connect/token", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Login failed ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        _accessToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("access_token missing from token response.");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public async Task<IReadOnlyList<RecipeOverviewDto>> GetRecipesAsync(CancellationToken ct = default)
    {
        EnsureAuthenticated();
        var payload = await _http.GetFromJsonAsync<RecipeListDto>("api/v1/recipes", ct)
            ?? throw new InvalidOperationException("Empty recipe list response.");
        return payload.Items;
    }

    private void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
        {
            throw new InvalidOperationException("Not authenticated. Call LoginAsync first.");
        }
    }
}

public sealed class RecipeListDto
{
    [JsonPropertyName("items")]
    public List<RecipeOverviewDto> Items { get; init; } = [];
}

public sealed class RecipeOverviewDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
}
