using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Application.Tests;

/// <summary>Test double for authenticated ownership (Identity user id).</summary>
public sealed class FixedCurrentUser(string? userId, string? userName = null) : ICurrentUser
{
    public string? UserId => userId;

    public string? UserName => userName ?? userId;

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(userId);
}
