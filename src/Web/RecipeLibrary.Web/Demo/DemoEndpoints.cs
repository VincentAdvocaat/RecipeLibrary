using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Infrastructure.Identity;
using RecipeLibrary.Web.Services;

namespace RecipeLibrary.Web.Demo;

public static class DemoEndpoints
{
    public const string MarkerCookie = "RecipeLibrary.Demo";

    public static void MapDemo(this WebApplication app)
    {
        app.MapPost("/demo/start", async (HttpContext http, IAntiforgery antiforgery,
            DemoIdentity identity, DemoOperationRunner guard, DemoSessionService sessions,
            SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            await antiforgery.ValidateRequestAsync(http);
            if (identity.IsAuthenticated && !identity.IsDemo) return Results.LocalRedirect("/demo");
            if (identity.IsDemo)
            {
                try { await guard.GetValidSessionAsync(ct); return Results.LocalRedirect("/recipes"); }
                catch (DemoAccessException) { /* Explicitly starting a fresh session after expiry. */ }
            }
            var session = await sessions.StartAsync(http.Connection.RemoteIpAddress?.ToString() ?? "unknown", ct);
            await SignInAsync(http, session, signIn, users);
            return Results.LocalRedirect("/recipes");
        }).AllowAnonymous();

        app.MapPost("/demo/reset", async (HttpContext http, IAntiforgery antiforgery,
            DemoIdentity identity, DemoSessionService sessions, SignInManager<ApplicationUser> signIn,
            UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            await antiforgery.ValidateRequestAsync(http);
            if (!identity.IsDemo || identity.UserId is null) return Results.Forbid();
            var session = await sessions.ResetAsync(identity.UserId, identity.Revision, ct);
            await SignInAsync(http, session, signIn, users);
            return Results.LocalRedirect("/recipes");
        }).RequireAuthorization();

        app.MapPost("/demo/end", async (HttpContext http, IAntiforgery antiforgery,
            DemoIdentity identity, DemoSessionService sessions, SignInManager<ApplicationUser> signIn, CancellationToken ct) =>
        {
            await antiforgery.ValidateRequestAsync(http);
            if (!identity.IsDemo || identity.UserId is null) return Results.Forbid();
            await sessions.EndAsync(identity.UserId, identity.Revision, ct);
            await signIn.SignOutAsync();
            ClearBrowserState(http);
            return Results.LocalRedirect("/demo?state=ended");
        }).RequireAuthorization();
    }

    private static async Task SignInAsync(HttpContext http, DemoSession session,
        SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users)
    {
        var user = await users.FindByIdAsync(session.UserId) ?? throw new InvalidOperationException("Demo identity missing.");
        await signIn.SignInWithClaimsAsync(user, new AuthenticationProperties
        {
            IsPersistent = true, AllowRefresh = false, IssuedUtc = session.CreatedAt, ExpiresUtc = session.ExpiresAt
        }, [new Claim(DemoIdentity.DemoClaim, "true"), new Claim(DemoIdentity.ExpiresClaim, session.ExpiresAt.ToString("O", CultureInfo.InvariantCulture)), new Claim(DemoIdentity.RevisionClaim, session.Revision.ToString(CultureInfo.InvariantCulture))]);
        ClearBrowserState(http);
        http.Response.Cookies.Append(MarkerCookie, "true", new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = http.Request.IsHttps,
            IsEssential = true, Path = "/", Expires = session.ExpiresAt.AddDays(1)
        });
    }

    public static void ClearBrowserState(HttpContext http)
    {
        http.Response.Cookies.Delete(ShoppingListSessionService.GroupIdCookieName, new CookieOptions { Path = "/" });
        http.Response.Cookies.Delete(MarkerCookie, new CookieOptions { Path = "/" });
    }
}
