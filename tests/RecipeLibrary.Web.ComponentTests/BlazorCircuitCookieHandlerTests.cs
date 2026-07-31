using System.Net.Http;
using Microsoft.AspNetCore.Http;
using RecipeLibrary.Web.Services;
using Xunit;

namespace RecipeLibrary.Web.ComponentTests;

public sealed class BlazorCircuitCookieHandlerTests
{
    [Fact]
    public async Task SendAsync_forwards_cookie_header_from_http_context()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = ".AspNetCore.Identity.Application=test-cookie";
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var inner = new CaptureHandler();
        var sut = new BlazorCircuitCookieHandler(accessor) { InnerHandler = inner };

        using var client = new HttpClient(sut) { BaseAddress = new Uri("https://localhost/") };
        _ = await client.GetAsync("/recipes/import-url");

        Assert.Equal(".AspNetCore.Identity.Application=test-cookie", inner.CookieHeader);
    }

    [Fact]
    public async Task SendAsync_omits_cookie_header_when_http_context_has_none()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var inner = new CaptureHandler();
        var sut = new BlazorCircuitCookieHandler(accessor) { InnerHandler = inner };

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
