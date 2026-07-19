using Microsoft.AspNetCore.Components;

using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.Localization;

using RecipeLibrary.Application.Abstractions;

using RecipeLibrary.Application.Contracts;

using RecipeLibrary.Resources;



namespace RecipeLibrary.Web.Services;



public sealed class ShoppingListSessionService(

    IHttpContextAccessor httpContextAccessor,

    IQueryBus queryBus,

    ICurrentUser userContext,

    IStringLocalizer<SharedResources> localizer,

    NavigationManager navigation)

{

    public const string GroupIdCookieName = "ShoppingListGroupId";

    public const string SetSessionPath = "/shopping-list/session/set";

    public const string ClearSessionPath = "/shopping-list/session/clear";



    public Guid? GetGroupIdFromCookie()

    {

        var context = httpContextAccessor.HttpContext;

        if (context?.Request.Cookies.TryGetValue(GroupIdCookieName, out var value) == true

            && Guid.TryParse(value, out var groupId))

        {

            return groupId;

        }



        return null;

    }



    public string BuildSetSessionUrl(Guid groupId, string redirectUri) =>

        $"{SetSessionPath}?groupId={groupId}&redirectUri={Uri.EscapeDataString(NormalizeRedirect(redirectUri))}";



    public string BuildClearSessionUrl(string redirectUri = "/recipes") =>

        $"{ClearSessionPath}?redirectUri={Uri.EscapeDataString(NormalizeRedirect(redirectUri))}";



    public static string NormalizeRedirect(string? redirectUri) =>
        RecipeLibrary.Application.Security.LocalRedirect.Normalize(redirectUri, fallback: "/recipes");



    public static CookieOptions CreateGroupCookieOptions() =>

        new()

        {

            Expires = DateTimeOffset.UtcNow.AddYears(1),

            IsEssential = true,

            SameSite = SameSiteMode.Lax,

            HttpOnly = true,

            Path = "/",

        };



    public bool TrySetGroupIdCookie(Guid groupId)

    {

        var context = httpContextAccessor.HttpContext;

        if (context is null || context.Response.HasStarted)

        {

            return false;

        }



        context.Response.Cookies.Append(GroupIdCookieName, groupId.ToString(), CreateGroupCookieOptions());

        return true;

    }



    public void RedirectToSetGroupCookie(Guid groupId, string redirectUri) =>

        navigation.NavigateTo(BuildSetSessionUrl(groupId, redirectUri), forceLoad: true);



    public void RedirectToClearSession(string redirectUri = "/recipes") =>

        navigation.NavigateTo(BuildClearSessionUrl(redirectUri), forceLoad: true);



    public async Task<GetOrCreateShoppingListGroupResult> GetOrCreateGroupAsync(CancellationToken ct = default)

    {

        var groupId = GetGroupIdFromCookie();

        var result = await queryBus.QueryAsync<GetOrCreateShoppingListGroupQuery, GetOrCreateShoppingListGroupResult>(

            new GetOrCreateShoppingListGroupQuery

            {

                GroupId = groupId,

                OwnerUserId = userContext.UserId,

                DefaultListNameFormat = localizer["ShoppingList.NumberedNameFormat"].Value,

            },

            ct);



        if (groupId != result.GroupId)

        {

            if (!TrySetGroupIdCookie(result.GroupId))

            {

                RedirectToSetGroupCookie(result.GroupId, navigation.Uri);

            }

        }



        return result;

    }



    public async Task<string> GetNextListNameAsync(Guid? scopeGroupId = null, CancellationToken ct = default)
    {
        var result = await queryBus.QueryAsync<GetNextShoppingListNameQuery, GetNextShoppingListNameResult>(
            new GetNextShoppingListNameQuery
            {
                NameFormat = localizer["ShoppingList.NumberedNameFormat"].Value,
                ScopeGroupId = scopeGroupId,
            },
            ct);

        return result.Name;
    }

    public async Task<int> GetUncheckedCountAsync(Guid groupId, CancellationToken ct = default)

    {

        var summary = await queryBus.QueryAsync<GetShoppingListSummaryQuery, ShoppingListSummaryResult>(

            new GetShoppingListSummaryQuery { GroupId = groupId },

            ct);



        return summary.UncheckedItemCount;

    }

}


