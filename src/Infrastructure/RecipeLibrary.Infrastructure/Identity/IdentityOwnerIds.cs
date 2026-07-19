namespace RecipeLibrary.Infrastructure.Identity;

/// <summary>
/// Shared column sizing for Identity user ids and owner foreign keys.
/// Matches ASP.NET Core Identity's default string key max length.
/// </summary>
public static class IdentityOwnerIds
{
    public const int MaxLength = 450;
}
