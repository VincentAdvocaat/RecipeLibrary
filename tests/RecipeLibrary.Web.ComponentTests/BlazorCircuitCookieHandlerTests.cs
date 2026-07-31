using System.Net.Http;
using RecipeLibrary.Web.Services;
using Xunit;

namespace RecipeLibrary.Web.ComponentTests;

public sealed class BlazorCircuitCookieHandlerTests
{
    [Fact]
    public async Task SendAsync_forwards_cookie_captured_at_construction()
    {
        var inner = new CaptureHandler();
        var sut = new BlazorCircuitCookieHandler(".AspNetCore.Identity.Application=test-cookie")
        {
            InnerHandler = inner,
        };

        using var client = new HttpClient(sut) { BaseAddress = new Uri("https://localhost/") };
        _ = await client.GetAsync("/recipes/import-url");

        Assert.Equal(".AspNetCore.Identity.Application=test-cookie", inner.CookieHeader);
    }

    [Fact]
    public async Task SendAsync_omits_cookie_header_when_none_was_captured()
    {
        var inner = new CaptureHandler();
        var sut = new BlazorCircuitCookieHandler(null) { InnerHandler = inner };

        using var client = new HttpClient(sut) { BaseAddress = new Uri("https://localhost/") };
        _ = await client.GetAsync("/ping");

        Assert.Null(inner.CookieHeader);
    }

    [Fact]
    public async Task SendAsync_omits_cookie_header_when_captured_cookie_is_empty()
    {
        var inner = new CaptureHandler();
        var sut = new BlazorCircuitCookieHandler(string.Empty) { InnerHandler = inner };

        using var client = new HttpClient(sut) { BaseAddress = new Uri("https://localhost/") };
        _ = await client.GetAsync("/ping");

        Assert.Null(inner.CookieHeader);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? CookieHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CookieHeader = request.Headers.TryGetValues("Cookie", out var values)
                ? string.Join("; ", values)
                : null;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
