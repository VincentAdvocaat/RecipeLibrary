using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Application.Tests;

/// <summary>Test double for authenticated shopping-list ownership (Entra OID simulation).</summary>
public sealed class FixedShoppingListUserContext(string? ownerUserId) : IShoppingListUserContext
{
    public string? OwnerUserId => ownerUserId;
}
