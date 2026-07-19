namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Authenticated user context for Application handlers (no HttpContext / Identity types).
/// </summary>
public interface ICurrentUser
{
    /// <summary>ASP.NET Core Identity user id when authenticated; otherwise null.</summary>
    string? UserId { get; }

    /// <summary>Public username when authenticated; otherwise null.</summary>
    string? UserName { get; }

    bool IsAuthenticated { get; }
}
