using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Application.Tests;

internal sealed class AnonymousCurrentUser : ICurrentUser
{
    public string? UserId => null;

    public string? UserName => null;

    public bool IsAuthenticated => false;
}
