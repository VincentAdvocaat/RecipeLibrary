namespace RecipeLibrary.Domain.Entities;

public sealed class Tag
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public ICollection<IngredientTag> IngredientTags { get; set; } = new List<IngredientTag>();
}
