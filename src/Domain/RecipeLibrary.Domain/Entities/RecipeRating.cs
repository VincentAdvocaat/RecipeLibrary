namespace RecipeLibrary.Domain.Entities;

public sealed class RecipeRating
{
    public Guid RecipeId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public int Stars { get; set; }
}
