using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.Localization;

using RecipeLibrary.Application.Abstractions;

using RecipeLibrary.Application.Contracts;

using RecipeLibrary.Resources;



namespace RecipeLibrary.Web.Services;



public sealed class ShoppingListSessionService(

    IQueryBus queryBus,
    ICommandBus commandBus,

    ICurrentUser userContext,

    IStringLocalizer<SharedResources> localizer)

{

    public const string GroupIdCookieName = "ShoppingListGroupId";

    public const string SetSessionPath = "/shopping-list/session/set";

    public const string ClearSessionPath = "/shopping-list/session/clear";



    public static string NormalizeRedirect(string? redirectUri) =>
        RecipeLibrary.Application.Security.LocalRedirect.Normalize(redirectUri, fallback: "/recipes");



    public static CookieOptions CreateGroupCookieOptions(bool secure) =>
        new()
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            HttpOnly = true,
            Secure = secure,
            Path = "/",
        };



    public async Task<EnsureShoppingListGroupResult> GetOrCreateGroupAsync(CancellationToken ct = default)

    {

        return await commandBus.SendAsync<EnsureShoppingListGroupCommand, EnsureShoppingListGroupResult>(

            new EnsureShoppingListGroupCommand

            {

                OwnerUserId = userContext.UserId,

                DefaultListNameFormat = localizer["ShoppingList.NumberedNameFormat"].Value,

            },

            ct);
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


