using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using RecipeLibrary.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace RecipeLibrary.Web.Auth;

public static class OpenIddictTokenEndpoints
{
    public static IEndpointRouteBuilder MapOpenIddictTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/token", ExchangeAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> ExchangeAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager)
    {
        var request = httpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsPasswordGrantType())
        {
            return await HandlePasswordAsync(request, userManager, signInManager, scopeManager);
        }

        if (request.IsRefreshTokenGrantType())
        {
            return await HandleRefreshAsync(httpContext, request, userManager, signInManager, scopeManager);
        }

        return ForbidOpenIddict(
            Errors.UnsupportedGrantType,
            "The specified grant type is not supported.");
    }

    private static async Task<IResult> HandlePasswordAsync(
        OpenIddictRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager)
    {
        var user = await userManager.FindByNameAsync(request.Username ?? string.Empty);
        if (user is null && !string.IsNullOrWhiteSpace(request.Username))
        {
            user = await userManager.FindByEmailAsync(request.Username);
        }

        if (user is null)
        {
            return ForbidOpenIddict(Errors.InvalidGrant, "The username/password couple is invalid.");
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password ?? string.Empty,
            lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return ForbidOpenIddict(Errors.InvalidGrant, "The username/password couple is invalid.");
        }

        var scopes = request.GetScopes();
        if (scopes.Length == 0)
        {
            scopes = [OpenIddictAppConstants.ApiScope, Scopes.OfflineAccess];
        }

        var principal = await CreatePrincipalAsync(user, scopes, userManager, signInManager, scopeManager);
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> HandleRefreshAsync(
        HttpContext httpContext,
        OpenIddictRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager)
    {
        var result = await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (result.Principal is null)
        {
            return ForbidOpenIddict(Errors.InvalidGrant, "The refresh token is no longer valid.");
        }

        var user = await userManager.GetUserAsync(result.Principal);
        if (user is null)
        {
            return ForbidOpenIddict(Errors.InvalidGrant, "The refresh token is no longer valid.");
        }

        if (!await signInManager.CanSignInAsync(user))
        {
            return ForbidOpenIddict(Errors.InvalidGrant, "The user is no longer allowed to sign in.");
        }

        var scopes = request.GetScopes();
        if (scopes.Length == 0)
        {
            scopes = result.Principal.GetScopes();
        }

        var principal = await CreatePrincipalAsync(user, scopes, userManager, signInManager, scopeManager);
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<ClaimsPrincipal> CreatePrincipalAsync(
        ApplicationUser user,
        ImmutableArray<string> scopes,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        var userId = await userManager.GetUserIdAsync(user);

        principal.SetClaim(Claims.Subject, userId);
        principal.SetClaim(ClaimTypes.NameIdentifier, userId);

        var userName = await userManager.GetUserNameAsync(user);
        if (!string.IsNullOrWhiteSpace(userName))
        {
            principal.SetClaim(Claims.Name, userName);
        }

        var email = await userManager.GetEmailAsync(user);
        if (!string.IsNullOrWhiteSpace(email))
        {
            principal.SetClaim(Claims.Email, email);
        }

        principal.SetScopes(scopes);
        principal.SetResources(await GetResourcesAsync(scopeManager, principal.GetScopes()));

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim, principal));
        }

        return principal;
    }

    private static async Task<IEnumerable<string>> GetResourcesAsync(
        IOpenIddictScopeManager scopeManager,
        ImmutableArray<string> scopes)
    {
        var resources = new List<string>();
        await foreach (var resource in scopeManager.ListResourcesAsync(scopes))
        {
            resources.Add(resource);
        }

        if (resources.Count == 0)
        {
            resources.Add("recipe_library_api");
        }

        return resources;
    }

    private static IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
    {
        switch (claim.Type)
        {
            case Claims.Name or ClaimTypes.Name:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Profile))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Email or ClaimTypes.Email:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Email))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Role or ClaimTypes.Role:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Roles))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case "AspNet.Identity.SecurityStamp":
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }

    private static IResult ForbidOpenIddict(string error, string description) =>
        Results.Forbid(
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
