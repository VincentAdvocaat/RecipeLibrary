using System.Net;
using System.Net.Http.Json;
using RecipeLibrary.App.Services;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class RecipeApiClientStartupTests
{
    [Fact]
    public async Task Login_SendsCredentialsOnlyAfterExplicitStartAndReady()
    {
        var paths = new List<string>();
        var polls = 0;
        using var http = CreateHttp(async (request, ct) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            paths.Add(path);
            if (path.EndsWith("start-token")) return Json(new { token = "csrf" });
            if (path.EndsWith("/start"))
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("csrf", Assert.Single(request.Headers.GetValues("RequestVerificationToken")));
                Assert.Equal("user", Assert.Single(request.Headers.GetValues("X-Application-Start")));
                Assert.Null(request.Content);
                return Json(new { state = "Starting" });
            }
            if (path.EndsWith("readiness")) return Json(new { state = ++polls == 2 ? "Ready" : "Starting" });
            Assert.Equal("/connect/token", path);
            Assert.Equal(2, polls);
            Assert.Contains("password=example", await request.Content!.ReadAsStringAsync(ct));
            return Json(new { access_token = "access" });
        });
        var client = new RecipeApiClient(http, TimeSpan.FromSeconds(5), TimeSpan.Zero);
        await client.LoginAsync("user", "example");
        Assert.True(client.IsAuthenticated);
        Assert.Equal(new[] { "/api/system/start-token", "/api/system/start", "/api/system/readiness", "/api/system/start", "/api/system/readiness", "/connect/token" }, paths);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Unknown")]
    public async Task FailedStartup_NeverSendsCredentials(string state)
    {
        using var http = CreateHttp((request, _) =>
        {
            Assert.DoesNotContain("connect/token", request.RequestUri!.AbsolutePath);
            return Task.FromResult(request.RequestUri.AbsolutePath.EndsWith("start-token")
                ? Json(new { token = "csrf" }) : Json(new { state }));
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RecipeApiClient(http).LoginAsync("user", "example"));
    }

    [Fact]
    public async Task Timeout_IsBoundedAndNeverSendsCredentials()
    {
        using var http = CreateHttp((request, _) =>
        {
            Assert.DoesNotContain("connect/token", request.RequestUri!.AbsolutePath);
            return Task.FromResult(request.RequestUri.AbsolutePath.EndsWith("start-token")
                ? Json(new { token = "csrf" }) : Json(new { state = "Starting" }));
        });
        var client = new RecipeApiClient(http, TimeSpan.FromMilliseconds(50), TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<TimeoutException>(() => client.LoginAsync("user", "example"));
    }

    [Theory]
    [InlineData("/api/system/start-token")]
    [InlineData("/api/system/start")]
    [InlineData("/api/system/readiness")]
    public async Task Cancellation_ReachesHttpAndNeverSendsCredentials(string cancelPath)
    {
        using var cancellation = new CancellationTokenSource();
        using var http = CreateHttp(async (request, ct) =>
        {
            Assert.DoesNotContain("connect/token", request.RequestUri!.AbsolutePath);
            if (request.RequestUri.AbsolutePath == cancelPath)
            {
                cancellation.Cancel();
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            return request.RequestUri.AbsolutePath.EndsWith("start-token")
                ? Json(new { token = "csrf" }) : Json(new { state = "Starting" });
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RecipeApiClient(http).LoginAsync("user", "example", cancellation.Token));
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private static HttpClient CreateHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new Handler(send)) { BaseAddress = new Uri("https://app.example/") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
}
