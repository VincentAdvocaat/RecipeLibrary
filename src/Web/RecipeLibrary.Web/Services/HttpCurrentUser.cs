using System.Security.Claims;
using OpenIddict.Abstractions;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Web.Services;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(OpenIddictConstants.Claims.Subject)
                ?? user.GetClaim(OpenIddictConstants.Claims.Subject);
        }
    }

    public string? UserName
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return user.Identity?.Name
                ?? user.FindFirstValue(OpenIddictConstants.Claims.Name)
                ?? user.GetClaim(OpenIddictConstants.Claims.Name);
        }
    }

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
}
