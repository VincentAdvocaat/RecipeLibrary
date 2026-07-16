using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Domain.Entities;

public sealed class ShoppingListItem
{
    public Guid Id { get; set; }

    public Guid ShoppingListId { get; set; }

    public Guid? CanonicalIngredientId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? Preparation { get; set; }

    /// <summary>Null when unmeasured (e.g. naar smaak).</summary>
    public Quantity? Quantity { get; set; }

    /// <summary>Null when unmeasured (e.g. naar smaak).</summary>
    public Unit? Unit { get; set; }

    public bool IsChecked { get; set; }

    public int SortOrder { get; set; }

    public ShoppingList? ShoppingList { get; set; }

    public ICollection<ShoppingListItemSource> Sources { get; set; } = new List<ShoppingListItemSource>();
}
