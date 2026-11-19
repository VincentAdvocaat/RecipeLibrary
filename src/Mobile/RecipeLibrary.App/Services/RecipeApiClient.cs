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

    private readonly TimeSpan _startupTimeout;
    private readonly TimeSpan _pollInterval;

    public RecipeApiClient(HttpClient http) : this(http, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(5)) { }

    internal RecipeApiClient(HttpClient http, TimeSpan startupTimeout, TimeSpan pollInterval)
    {
        _http = http;
        _startupTimeout = startupTimeout;
        _pollInterval = pollInterval;
    }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);

    public async Task LoginAsync(string userName, string password, CancellationToken ct = default)
    {
        await WaitForApplicationAsync(ct);
        ct.ThrowIfCancellationRequested();
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

    private async Task WaitForApplicationAsync(CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(_startupTimeout);
        var startupToken = deadline.Token;
        try
        {
            string? token = null;
            while (true)
            {
                try
                {
                    // The shared handler keeps the antiforgery cookie across these requests.
                    if (token is null)
                    {
                        using var tokenResponse = await _http.GetAsync("api/system/start-token", startupToken);
                        tokenResponse.EnsureSuccessStatusCode();
                        using var tokenDocument = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(startupToken));
                        token = tokenDocument.RootElement.GetProperty("token").GetString();
                        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Application start token is missing.");
                    }
                    using var start = new HttpRequestMessage(HttpMethod.Post, "api/system/start");
                    start.Headers.Add("RequestVerificationToken", token);
                    start.Headers.Add("X-Application-Start", "user");
                    using var startResponse = await _http.SendAsync(start, startupToken);
                    startResponse.EnsureSuccessStatusCode();
                    using var readiness = await _http.GetAsync("api/system/readiness", startupToken);
                    readiness.EnsureSuccessStatusCode();
                    using var stateDocument = JsonDocument.Parse(await readiness.Content.ReadAsStringAsync(startupToken));
                    var state = stateDocument.RootElement.GetProperty("state").GetString();
                    if (state == "Ready") return;
                    if (state == "Failed") throw new InvalidOperationException("The recipe library could not start. Please try again later.");
                    if (state != "Starting") throw new InvalidOperationException("Unexpected application startup state.");
                }
                catch (HttpRequestException error) when (error.StatusCode is null
                    or System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.BadGateway
                    or System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.GatewayTimeout)
                {
                    // Cold ingress and network interruptions share the original bounded deadline.
                }
                catch (OperationCanceledException) when (!startupToken.IsCancellationRequested) { }
                catch (JsonException) { }
                await Task.Delay(_pollInterval, startupToken);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("The recipe library took too long to start. Please try again later.");
        }
    }

    public async Task<IReadOnlyList<RecipeOverviewDto>> GetRecipesAsync(CancellationToken ct = default)
    {
        EnsureAuthenticated();
        await WaitForApplicationAsync(ct);
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
