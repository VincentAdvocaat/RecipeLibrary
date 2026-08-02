using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Application.Tests;

/// <summary>Test double for authenticated ownership (Identity user id).</summary>
public sealed class FixedCurrentUser(string? userId, string? userName = null, params string[] roles) : ICurrentUser
{
    private readonly HashSet<string> _roles = new(roles ?? [], StringComparer.OrdinalIgnoreCase);

    public string? UserId => userId;

    public string? UserName => userName ?? userId;

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(userId);

    public bool IsInRole(string roleName) =>
        !string.IsNullOrWhiteSpace(roleName) && _roles.Contains(roleName);
}
