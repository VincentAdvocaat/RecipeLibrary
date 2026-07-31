using Microsoft.AspNetCore.Http;

namespace RecipeLibrary.Web.Services;

/// <summary>
/// Forwards the browser request Cookie header on Blazor Server loopback <see cref="HttpClient"/> calls.
/// Without this, authorized minimal APIs challenge unauthenticated requests and redirect to login,
/// which surfaces as a Content-Type error when the JSON POST is replayed against the login form.
/// </summary>
public sealed class BlazorCircuitCookieHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var cookie = httpContextAccessor.HttpContext?.Request.Headers.Cookie.ToString();
        if (!string.IsNullOrEmpty(cookie))
        {
            request.Headers.Remove("Cookie");
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
