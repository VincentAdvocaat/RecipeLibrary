namespace RecipeLibrary.Web.Services;

/// <summary>
/// Forwards a captured browser Cookie header on Blazor Server loopback <see cref="HttpClient"/> calls.
/// The cookie must be captured when the scoped client is created (while <c>HttpContext</c> is still
/// available). Reading <c>IHttpContextAccessor</c> on each send is unreliable after the circuit starts.
/// Without cookies, authorized minimal APIs challenge unauthenticated requests and redirect to login,
/// which surfaces as a Content-Type error when the JSON POST is replayed against the login form.
/// </summary>
public sealed class BlazorCircuitCookieHandler(string? cookie) : DelegatingHandler
{
    private readonly string? _cookie = string.IsNullOrEmpty(cookie) ? null : cookie;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_cookie is not null)
        {
            request.Headers.Remove("Cookie");
            request.Headers.TryAddWithoutValidation("Cookie", _cookie);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
