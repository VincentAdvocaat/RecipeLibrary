namespace RecipeLibrary.Application.Abstractions;

public interface IShoppingListUserContext
{
    /// <summary>Entra object ID when user-scoped shopping lists are active; otherwise null.</summary>
    string? OwnerUserId { get; }
}
