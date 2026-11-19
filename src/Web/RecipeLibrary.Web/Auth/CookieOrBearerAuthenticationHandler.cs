using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;

namespace RecipeLibrary.Web.Auth;

/// <summary>
/// Authenticates with OpenIddict Bearer when present and valid; otherwise falls back to the
/// Identity application cookie (so a Blazor session still works if a stale Bearer header is sent).
/// </summary>
public sealed class CookieOrBearerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(header)
            && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var bearer = await Context.AuthenticateAsync(
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            if (bearer.Succeeded)
            {
                return bearer;
            }

            var cookie = await Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (cookie.Succeeded)
            {
                return cookie;
            }

            // Prefer Bearer failure so API clients get 401 semantics instead of a login redirect.
            return bearer;
        }

        return await Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
    }
}
