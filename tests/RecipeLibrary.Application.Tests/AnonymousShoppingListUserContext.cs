using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Application.Tests;

internal sealed class AnonymousShoppingListUserContext : IShoppingListUserContext
{
    public string? OwnerUserId => null;
}
