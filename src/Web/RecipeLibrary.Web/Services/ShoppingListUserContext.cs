using System.Security.Claims;
using Microsoft.Identity.Web;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Web.Services;

public sealed class AnonymousShoppingListUserContext : IShoppingListUserContext
{
    public string? OwnerUserId => null;
}

public sealed class HttpShoppingListUserContext(IHttpContextAccessor httpContextAccessor) : IShoppingListUserContext
{
    public string? OwnerUserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return user.FindFirstValue(ClaimConstants.ObjectId)
                ?? user.FindFirstValue("oid")
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}
