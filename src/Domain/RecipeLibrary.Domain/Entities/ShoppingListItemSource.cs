namespace RecipeLibrary.Domain.Entities;

public sealed class ShoppingListItemSource
{
    public Guid ShoppingListItemId { get; set; }

    public Guid RecipeId { get; set; }

    public string RecipeTitle { get; set; } = string.Empty;

    public ShoppingListItem? Item { get; set; }
}
